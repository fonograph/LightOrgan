import mido
import pygame
import time
import math
import serial
import pyenttec 
import sys
import argparse
import socketserver
import os
sys.path.insert(0, '/Projects/psmoveapi/build');
import psmove
from serial.tools import list_ports
from mido import Message

parser = argparse.ArgumentParser()
parser.add_argument('--server', action='store_const', const=True)
parser.add_argument('--unity', action='store_const', const=True)
args = parser.parse_args()

print(mido.get_output_names())
print([port.device for port in list_ports.comports()])

pygame.init()
pygame.display.set_mode((500, 500))
clock = pygame.time.Clock()

MIN_PRESSURE = 15.7
MAX_PRESSURE = 16.5
notes = [
	[48, 52, 55, 59], # C
	[55, 59, 62, 66], # G
	[60, 64, 67, 71], # C 
	[65, 69, 72, 76], # F
]
colors = [
	[[0,0,255,0], [255,0,127,0], [0,255,0,0], [127,0,255,0]],
	[[0,255,0], [0,255,255,0], [0,0,255,0], [127,255,0,0]],
	[[255,0,0,0], [127,255,0,0], [0,255,255,0], [255,127,0,0]],
	[[255,255,0,0], [255,0,255,0], [255,127,0,0], [255,0,0,0]],
]
lightBars = 2 # total sets of 4 lights to iterate over
lightTotalChannels = 5 # DMX channels per light
lightIntensityChannel = 4  # within the local addresses
lightColorChannel = [0,1,2,None] # r g b w
whiteLights = []  # indexed across all bars, e.g. the 5th light would be the 1st light on bar 2
whiteLevel = 50
modeTime = 30 # seconds
unityControlsLevels = False

mode = 0
levels = [0, 0, 0, 0]

midi = mido.open_output('IAC Driver Bus 1')

ser = None
for port in list_ports.comports():
	if 'usbmodem' in port.device:			
		ser = serial.Serial(port.device, 115200)
if ser == None:
	print("\nCOULD NOT FIND SERIAL\n")

dmx = pyenttec.select_port() 

controller = None
if psmove.count_connected() > 0:
	controller = psmove.PSMove(0)

debugChannel = 0

lastModeSwitchTime = 0



def pressureToValue(pressure):
	normalized = (pressure - MIN_PRESSURE) / (MAX_PRESSURE - MIN_PRESSURE)
	normalized = min(1, max(0, normalized))
	return normalized * 127

def setHigherLevel(channel, value):
	if value > levels[channel]:
		setLevel(channel, value)

def setLevel(channel, value):
	levels[channel] = value
	setSoundLevel(channel, value)
	setLightLevel(channel, value)

def setSoundLevel(channel, value):
	midi.send(Message('aftertouch', channel=channel, value=round(value)))	

def setLightLevel(channel, value):
	if dmx:
		for bar in range(lightBars):
			start = bar * (lightTotalChannels*len(colors[mode])) + channel * lightTotalChannels

			if value == 0 and (bar * len(colors[mode]) + channel) in whiteLights:
				dmx.set_channel(start + 0, 0)
				dmx.set_channel(start + 1, 0)
				dmx.set_channel(start + 2, 0)
				dmx.set_channel(start + 3, 255)
				dmx.set_channel(start + 4, whiteLevel)
				continue

			intensity = pow(value/127, 0.5)
			if lightIntensityChannel is not None:
				dmx.set_channel(start + lightIntensityChannel, round(255 * intensity)) #intensity
				#print(start + lightIntensityChannel, round(255 * intensity))
			for i in range(len(colors[mode][channel])):
				if lightColorChannel[i] is not None:
					val = colors[mode][channel][i]
					if lightIntensityChannel is None:
						val = round(intensity * val)
					dmx.set_channel(start + lightColorChannel[i], val)
					#print(start + lightColorChannel[i], val)


lastMidiNotesSent = [-1,-1,-1,-1] # used to make sure we don't re-send repeated notes
def setMode(m):
	global mode
	global lastModeSwitchTime
	mode = m
	lastModeSwitchTime = pygame.time.get_ticks()
	for i in range(len(levels)):
		if lastMidiNotesSent[i] != notes[mode][i]:
			midi.send(Message('control_change', channel=i, control=123, value=0)) # all notes off
			midi.send(Message('note_on', channel=i, note=notes[mode][i]))
			lastMidiNotesSent[i] = notes[mode][i]
		setLevel(i, levels[i])

setMode(0)


def runApp(socket=None):
	print('Running app')
	appRunning = True
	while appRunning:

		delta = clock.tick(999)

		#fade
		allLevelsZero = True
		for i in range(len(levels)):
			value = max(levels[i] - (127 * 4 * (delta/1000)), 0)
			setLevel(i, value)
			allLevelsZero = allLevelsZero and value == 0

		#serial in
		if ser:
			#print(ser.in_waiting)
			while ser.in_waiting > 0:
				line = ser.readline()
				#print(line)
				try:
					pressures = line.decode().split(',')
					for i in range(len(pressures)):
						value = pressureToValue(float(pressures[i]))
						setHigherLevel(i, value)
				except:
					print('error in serial input')
					print(line)

		#mode switch (only when running standalone)
		if socket is None and allLevelsZero and pygame.time.get_ticks() > lastModeSwitchTime + modeTime*1000:
			nextMode = mode + 1
			if nextMode > 3:
				nextMode = 0
			setMode(nextMode)

		#socket 
		if socket:
			global notes
			global colors
			socket.wfile.write((','.join(map(str,levels)) + "\n").encode())
			colorLine = socket.rfile.readline()
			noteLine = socket.rfile.readline()
			onOffLine = socket.rfile.readline()
			#print(colorLine)
			#print(noteLine)
			#print(onOffLine)
			try:
				colorVals = list(map(int, colorLine.strip().split(b',')))
				noteVals = list(map(int, noteLine.strip().split(b',')))
				onOffVals = list(map(int, onOffLine.strip().split(b',')))
				# put received colors/notes in mode 1
				colors[0] = [colorVals[0:4], colorVals[4:8], colorVals[8:12], colorVals[12:16]]
				notes[0] = noteVals
				setMode(0)

				if (unityControlsLevels):
					for i in range(len(onOffVals)):
						setLevel(i, onOffVals[i] * 127)
			except:
				print('error in socket input')
				print(colorLine)
				print(noteLine)
				print(onOffLine)
			

		#psmove
		# if controller:
		# 	while controller.poll():
		# 		pressed, released = controller.get_button_events()
		# 		if pressed & psmove.Btn_SQUARE:
		# 			setMode(0)
		# 		if pressed & psmove.Btn_TRIANGLE:
		# 			setMode(1)
		# 		if pressed & psmove.Btn_CIRCLE:
		# 			setMode(2)
		# 		if pressed & psmove.Btn_CROSS:
		# 			setMode(3)
		# 	controller.set_leds(colors[mode][0][0],colors[mode][0][1],colors[mode][0][2])
		# 	controller.update_leds()

		events = pygame.event.get()
		for event in events:
		    if event.type == pygame.KEYDOWN:
		    	global debugChannel
		    	
		    	if event.key == pygame.K_ESCAPE:
		    		appRunning = False

		    	elif event.key == pygame.K_UP:
		    		setMode(0)
		    	elif event.key == pygame.K_RIGHT:
		    		setMode(1)
		    	elif event.key == pygame.K_DOWN:
		    		setMode(2)
		    	elif event.key == pygame.K_LEFT:
		    		setMode(3)

		    	elif event.key == pygame.K_q:
		    		debugChannel = 0
		    	elif event.key == pygame.K_w:
		    		debugChannel = 1
		    	elif event.key == pygame.K_e:
		    		debugChannel = 2
		    	elif event.key == pygame.K_r:
		    		debugChannel = 3
		    	elif event.key == pygame.K_1:
		    		setLevel(debugChannel, 127/9 * 0)
		    	elif event.key == pygame.K_2:
		    		setLevel(debugChannel, 127/9 * 1)
		    	elif event.key == pygame.K_3:
		    		setLevel(debugChannel, 127/9 * 2)
		    	elif event.key == pygame.K_4:
		    		setLevel(debugChannel, 127/9 * 3)
		    	elif event.key == pygame.K_5:
		    		setLevel(debugChannel, 127/9 * 4)
		    	elif event.key == pygame.K_6:
		    		setLevel(debugChannel, 127/9 * 5)
		    	elif event.key == pygame.K_7:
		    		setLevel(debugChannel, 127/9 * 6)
		    	elif event.key == pygame.K_8:
		    		setLevel(debugChannel, 127/9 * 7)
		    	elif event.key == pygame.K_9:
		    		setLevel(debugChannel, 127/9 * 8)
		    	elif event.key == pygame.K_0:
		    		setLevel(0, 127/9 * 9)
		    		setLevel(1, 127/9 * 9)
		    		setLevel(2, 127/9 * 9)
		    		setLevel(3, 127/9 * 9)

		if dmx:
			dmx.render()


	for i in range(len(levels)):
		midi.send(Message('control_change', channel=i, control=123, value=0)) # all notes off


class MyTCPHandler(socketserver.StreamRequestHandler):
	def handle(self):
		runApp(self)

if args.unity:
	os.system('open LightOrgan.app')
	     

if args.server:
	socketserver.TCPServer.allow_reuse_address = True
	with socketserver.TCPServer(('localhost', 9999), MyTCPHandler) as server:
        # stop with ctrl-C
		print('Starting server')
		server.serve_forever()
else:
	runApp()










import mido
import pygame
import time
import math
import serial
import pyenttec 
import sys
sys.path.insert(0, '/Projects/psmoveapi/build');
import psmove
from serial.tools import list_ports
from mido import Message

print(mido.get_output_names())
print([port.device for port in list_ports.comports()])

pygame.init()
pygame.display.set_mode((500, 500))
clock = pygame.time.Clock()

MIN_PRESSURE = 15
MAX_PRESSURE = 22
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
lightBars = 3  
whiteLights = [0, 10]  # indexed across all bars, e.g. the 5th light would be the 1st light on bar 2
whiteLevel = 50
modeTime = 30 # seconds

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
			start = bar * 20 + channel * 5

			if value == 0 and (bar * len(colors[mode]) + channel) in whiteLights:
				dmx.set_channel(start + 0, 0)
				dmx.set_channel(start + 1, 0)
				dmx.set_channel(start + 2, 0)
				dmx.set_channel(start + 3, 255)
				dmx.set_channel(start + 4, whiteLevel)
				continue

			dmx.set_channel(start + 4, round(255 * pow(value/127, 0.5))) #intensity
			for i in range(len(colors[mode][channel])):
				dmx.set_channel(start + i, colors[mode][channel][i])

def setMode(m):
	global mode
	global lastModeSwitchTime
	mode = m
	lastModeSwitchTime = pygame.time.get_ticks()
	for i in range(len(levels)):
		midi.send(Message('control_change', channel=i, control=123, value=0)) # all notes off
		midi.send(Message('note_on', channel=i, note=notes[mode][i]))
		setLevel(i, levels[i])

setMode(0)


appRunning = True
while appRunning:

	delta = clock.tick(999)

	allLevelsZero = True

	for i in range(len(levels)):
		value = max(levels[i] - (127 * 0.5 * (delta/1000)), 0)
		setLevel(i, value)
		allLevelsZero = allLevelsZero and value == 0

	if ser:
		while ser.in_waiting > 0:
			line = ser.readline()
			print(line)
			pressures = line.split(',')
			for i in range(len(pressures)):
				value = pressureToValue(float(pressures[i]))
				setHigherLevel(i, value)

	if allLevelsZero and pygame.time.get_ticks() > lastModeSwitchTime + modeTime*1000:
		nextMode = mode + 1
		if nextMode > 3:
			nextMode = 0
		setMode(nextMode)

	if controller:
		while controller.poll():
			pressed, released = controller.get_button_events()
			if pressed & psmove.Btn_SQUARE:
				setMode(0)
			if pressed & psmove.Btn_TRIANGLE:
				setMode(1)
			if pressed & psmove.Btn_CIRCLE:
				setMode(2)
			if pressed & psmove.Btn_CROSS:
				setMode(3)
		controller.set_leds(colors[mode][0][0],colors[mode][0][1],colors[mode][0][2])
		controller.update_leds()

	events = pygame.event.get()
	for event in events:
	    if event.type == pygame.KEYDOWN:
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
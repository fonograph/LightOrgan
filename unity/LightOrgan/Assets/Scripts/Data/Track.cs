using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEngine;

public class Track {
    public List<List<Note>> notes;
    public float endTime;

    public Track(string xml) {
        this.notes = new List<List<Note>>();
        this.notes.Add(new List<Note>());
        this.notes.Add(new List<Note>());
        this.notes.Add(new List<Note>());
        this.notes.Add(new List<Note>());

        this.endTime = 0;

        XElement root = XElement.Parse(xml);

        float startTime = Config.START_DELAY;

        int minNote = 99999;
        int maxNote = 0;

        int minVelocity = 99999;
        int maxVelocity = 0;

        // midi tracks
        for (int i=1; i<=4; i++) {
            Note lastNote = null;
            foreach (XElement keyframe in root.Descendants("track").Where(item => (string)item.Element("name") == "Pump "+i).Descendants("keyframe")) {
                float time = (int)keyframe.Element("time") * Config.TIME_VALUE + startTime;
                int value = (int)keyframe.Element("value");
                int velocity = (int)keyframe.Element("velocityOn");
                Note note = new Note(time, Config.SHORT_NOTE, velocity, value);
                this.notes[i-1].Add(note);

                minNote = System.Math.Min(minNote, value);
                maxNote = System.Math.Max(maxNote, value);
                minVelocity = System.Math.Min(minVelocity, velocity);
                maxVelocity = System.Math.Max(maxVelocity, velocity);

                // calculate end time with long note because it might get extended in the OSC track
                if (note.time + Config.LONG_NOTE > this.endTime) {
                    this.endTime = note.time + Config.LONG_NOTE;
                }   

                if (lastNote != null && lastNote.time + lastNote.length > note.time - 0.1f) {
                    lastNote.length = Mathf.Max(note.time - lastNote.time - 0.1f, 0.1f);
                    Debug.Log("note too long at " + time + " changed to " + lastNote.length);
                } 

                lastNote = note;
            }
        }

        Debug.Log("min note " + minNote);
        Debug.Log("max note " + maxNote);

        Debug.Log("min velocity " + minVelocity);
        Debug.Log("max velocity " + maxVelocity);

        foreach (List<Note> list in this.notes) {
            foreach (Note note in list) {
                note.UpdateColor(minNote, maxNote);
                if (maxVelocity - minVelocity == 0) {
                    note.volume = 1;
                } else {
                    note.volume = (float)(note.volume - minVelocity) / (float)(maxVelocity - minVelocity) * 0.7f + 0.3f;;
                }
            }
        }

        // osc track
        foreach (XElement keyframe in root.Descendants("track").Where(item => (string)item.Element("name") == "Pumps OSC").Descendants("keyframe")) {
            float time = (int)keyframe.Element("time") * Config.TIME_VALUE + startTime;
            string[] value = ((string)keyframe.Element("value")).Split(new char[]{'/'}, System.StringSplitOptions.RemoveEmptyEntries);
            int pumpIndex = int.Parse(value[0].Substring(4)) - 1;

            bool foundNote = false;
            foreach (Note note in this.notes[pumpIndex]) {
                if (note.time == time) {
                    note.length = value[1] == "long" ? Config.LONG_NOTE : Config.SHORT_NOTE;
                    foundNote = true;
                }
            }
            if (!foundNote) {
                Debug.LogWarning("Could not find OSC track note for time " + keyframe.Element("time") + " on pump " + value[0]);
            }
        }

        //this.endTime = 2;
    }

    // Gets the first note in every track that ends after the given time point
    public List<Note> getNotesForTime(float time) {
        List<Note> result = new List<Note>();
        foreach (List<Note> instrument in this.notes) {
            Note selectedNote = null;
            foreach (Note note in instrument) {
                if (note.time + note.length >= time) {
                    selectedNote = note;
                    break;
                }
            }
            result.Add(selectedNote);
        }
        return result;
    }
}
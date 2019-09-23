using UnityEngine;

public class Note {
    public float time;
    public float length;
    public float volume;
    public int note;
    public int[] rgbw;
    public Color color;
    public float endTime { get { return this.time + this.length; }}
    public Note(float time, float length, float volume, int note) {
        this.time = time;
        this.length = length;
        this.volume = volume;
        this.note = note;
    }

    public void UpdateColor(int minNote, int maxNote) {
        float h = ((((this.note-minNote)/((float)maxNote-(float)minNote)*360f) + 240) % 360) / 360;
        this.color = Color.HSVToRGB((1-Mathf.Clamp01(h)) * 0.75f, 1, 1);
        this.rgbw = new int[]{(int)(this.color.r * 255), (int)(this.color.g * 255), (int)(this.color.b * 255), 255};
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimpleTCP;
using DG.Tweening;

public class GameController : MonoBehaviour
{
    public static GameController _Instance;
    public static GameController Instance {
        get {
            return _Instance;
        }
    }

    public TextAsset[] trackXml;

    public AudioClip[] trackMp3;

    public Stream[] Streams;

    public Score Score;

    public SpriteRenderer Blackout;
    
    public TMPro.TextMeshPro StartText;

    public AudioSource Music;

    private SimpleTcpClient tcpClient;

    private List<Track> tracks;

    private bool inGame = false;
    private bool readyToStartGame = true;
    private bool freePlay = false;

    private int currentTrackIndex = 0;

    private float currentTrackTime;

    private float currentTrackStartTime;

    private List<Note> lastNotesSentToServer = null;

    private Track currentTrack {
        get {
            return this.tracks[this.currentTrackIndex % this.tracks.Count];
        }
    }

    private AudioClip currentTrackMp3 {
        get {
            return this.trackMp3[this.currentTrackIndex % this.tracks.Count];
        }
    }
   
    void Start() {
        GameController._Instance = this;

        this.tracks = new List<Track>();
        foreach (TextAsset xmlAsset in this.trackXml) {
            Track track = new Track(xmlAsset.text);
            this.tracks.Add(track);
        }

        try {
            this.tcpClient = new SimpleTcpClient();
            this.tcpClient.Connect("localhost", 9999);
            this.tcpClient.DelimiterDataReceived += this.HandlePumpDataReceived;
        } catch (System.Exception e) {
            Debug.LogError(e);
        }

        this.ReadyToStartGame();
    }

    void Update() {
        if (this.inGame) {
            this.currentTrackTime = Time.time - this.currentTrackStartTime;

            if (this.currentTrackTime >= this.currentTrack.endTime + 2) {
                Debug.Log("Track ended " + this.currentTrackTime + " " + this.currentTrack.endTime);
                this.StartCoroutine(this.EndGame());
            }

            this.SpawnPulses();
            foreach (Stream stream in this.Streams) {
                stream.UpdateForTime(this.currentTrackTime, Time.deltaTime);

                // just pumped?
                if (stream.lastPumpStartTime == Time.time) {
                    foreach (Stream otherStream in this.Streams) {
                        if (otherStream != stream && Time.time - otherStream.lastPumpStartTime < Config.PARALLEL_EFFECT_WINDOW) {
                            otherStream.ShowParallelEffect();
                            stream.ShowParallelEffect();
                        }
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Return) ) {
                this.ReadyToStartGame();
            }
        } else {
            if (this.readyToStartGame) {
                if (Input.GetKeyDown(KeyCode.Return) ) {
                    this.StartGame();
                }
                if (!this.freePlay && this.Streams.Where(stream => stream.Pump.Value > 0.01).Count() == this.Streams.Count()) {
                    this.StartGame();
                }
                if (Input.GetKeyDown(KeyCode.Space)) {
                    this.ToggleFreePlay();
                }
            }
        }

        // DEBUG CONTROLS
        if (Input.GetKey(KeyCode.Alpha1)) {
                this.FadePumpPressure(0, 1, true);
            } else {
                this.FadePumpPressure(0, 0);
            }
            if (Input.GetKey(KeyCode.Alpha2)) {
                this.FadePumpPressure(1, 1, true);
            } else {
                this.FadePumpPressure(1, 0);
            }
            if (Input.GetKey(KeyCode.Alpha3)) {
                this.FadePumpPressure(2, 1, true);
            } else {
                this.FadePumpPressure(2, 0);
            }
            if (Input.GetKey(KeyCode.Alpha4)) {
                this.FadePumpPressure(3, 1, true);
            } else {
                this.FadePumpPressure(3, 0);
            }
    }

    private void ReadyToStartGame() {
        this.inGame = false;
        this.readyToStartGame = true;
        this.freePlay = false;
        this.Score.Reset();
        this.Blackout.DOFade(0, 0.5f);
        this.StartText.gameObject.SetActive(true);
        this.StartText.color = new Color(1, 1, 1, 1);
        foreach (Stream stream in this.Streams) {
            stream.Reset();
        }
        this.Music.Stop();
    }

    private void StartGame() {
        if (!this.inGame && this.readyToStartGame) {
            this.Score.Reset();
            this.StartText.color = new Color(1, 1, 1, 0);

            this.currentTrackIndex++;
            this.currentTrackStartTime = Time.time;
            this.currentTrackTime = 0;

            this.Music.clip = this.currentTrackMp3;
            this.Music.PlayDelayed(Config.START_DELAY);

            this.inGame = true;
            this.readyToStartGame = false;
        }
    }

    private IEnumerator EndGame() {
        if (this.inGame) {
            this.inGame = false;

            this.Blackout.DOFade(1, 2).SetEase(Ease.InOutQuad);
            this.Score.EndTransition(2, 0);

            yield return new WaitForSeconds(5);

            this.ReadyToStartGame();
        }
    }

    private void ToggleFreePlay() {
        if (!this.inGame && this.readyToStartGame) {
            this.freePlay = !this.freePlay;
            this.StartText.gameObject.SetActive(!this.freePlay);
        }
    }

    private void HandlePumpDataReceived(object sender, Message message) {
        Debug.Log("in " + message.MessageString);
        //Debug.Log("time " + this.currentTrackTime);

        List<Note> notes = this.currentTrack.getNotesForTime(this.currentTrackTime);

        string[] levels = message.MessageString.Split(',');
        for (int i=0; i<4; i++) {
            float level = float.Parse(levels[i]) / 127;
            this.Streams[i].SetPumpPressure(level);

            // stick with previous note while pumping
            if (notes[i] == null || level > 0.01f) {
                notes[i] = this.lastNotesSentToServer[i];
            }
        }

        this.lastNotesSentToServer = new List<Note>(notes);

        Debug.Log(this.currentTrackTime);
        foreach (Note note in notes) {
            Debug.Log(note != null ? note.time+"" : "null");
        }

        string colorsString = string.Join(",", notes.Select(note => string.Join(",", note.rgbw)));
        string notesString = string.Join(",", notes.Select(note => note.note));
        string onOffString = string.Join(",", notes.Select(note => (note != null && note.time <= this.currentTrackTime) ? "1" : "0"));
        //Debug.Log("out " + colorsString + " " + notesString);
        this.tcpClient.WriteLine(colorsString);
        this.tcpClient.WriteLine(notesString);
        this.tcpClient.WriteLine(onOffString);
    }

    private void FadePumpPressure(int index, float pressure, bool instant = false) {
        if (!instant) {
            this.Streams[index].SetPumpPressure(this.Streams[index].Pump.Value + (pressure - this.Streams[index].Pump.Value) / 10);
        } else {
            this.Streams[index].SetPumpPressure(pressure);   
        }
    }

    private void SpawnPulses() {
        List<Note> notesToSpawn = this.currentTrack.getNotesForTime(this.currentTrackTime + Config.PULSE_TRAVEL_TIME_UP);
        for (int i=0; i<4; i++) {
            if (notesToSpawn[i] != null) {
                this.Streams[i].GeneratePulse(notesToSpawn[i]);
            }
        }
    }





}

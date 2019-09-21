using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stream : MonoBehaviour {

	public Pulse PulsePrefab;

	public Transform PulseOrigin;
	public Transform PulseDestination;
	public Transform PulseContainer;
	public Pump Pump;
	public StreamParticles particles;
	public Feedback feedback;

	[HideInInspector]
	public float? lastPumpStartTime;

	private List<Pulse> pulses = new List<Pulse>();

	private List<Note> spawnedNotes = new List<Note>();

	void Start() {
		for (int i=this.PulseContainer.childCount-1; i>=0; i--) {
			GameObject.Destroy(this.PulseContainer.GetChild(i));
		}
	}

	void OnDrawGizmos() {
		Gizmos.color = Color.red;
		Gizmos.DrawWireCube(
			(PulseDestination.position + PulseOrigin.position) / 2, 
			new Vector3(1, (PulseDestination.position.y - PulseOrigin.position.y), 0)
		);
	}

	public void Reset() {
		foreach (Pulse pulse in this.pulses) {
			GameObject.Destroy(pulse.gameObject);
		}
		this.pulses.Clear();
		this.spawnedNotes.Clear();
		this.particles.SetActive(false);
		this.lastPumpStartTime = null;
	}

	public void GeneratePulse(Note note) {
		if (!this.spawnedNotes.Contains(note)) {
			Pulse pulse = Instantiate(this.PulsePrefab, this.PulseContainer, false);
			pulse.Init(note, this.PulseOrigin.position, this.PulseDestination.position);
			pulse.OnKill += this.HandlePulseKilled;
			pulse.OnStartPump += this.HandlePulseStartPump;
			pulse.OnEarly += this.HandlePulseEarly;
			pulse.OnLate += this.HandlePulseLate;
			this.pulses.Add(pulse);
			this.spawnedNotes.Add(note);
		}
	}

	public void UpdateForTime(float time, float timeDelta) {
		if (this.pulses.Count > 0) {
			this.pulses[0].KillMaybe(time);
			if (this.pulses.Count == 0) {
				return;
			}
			foreach (Pulse pulse in this.pulses) {
				pulse.Move(time);
			}
			this.pulses[0].Pump(time, timeDelta, this.Pump.Value);
			this.particles.SetActive(this.pulses[0].IsBeingPumped, this.pulses[0].Note.endTime - time < 0.2f);
			this.particles.SetColor(this.pulses[0].Note.color);
			this.Pump.SetColor(this.pulses[0].Note.color);
		} else {
			this.particles.SetActive(false);
		}
	}

	public void SetPumpPressure(float value) {
		this.Pump.SetPressure(value);
	}

	public void ShowParallelEffect() {
		this.particles.ShowParallelEffect();
	}

	private void HandlePulseStartPump(Pulse pulse) {
		this.lastPumpStartTime = Time.time;
	}

	private void HandlePulseKilled(Pulse pulse) {
		this.pulses.Remove(pulse);
	}

	private void HandlePulseEarly(Pulse pulse) {
		this.feedback.Show("Early!");
	}

	private void HandlePulseLate(Pulse pulse) {
		this.feedback.Show("Late!");
	}

	
}

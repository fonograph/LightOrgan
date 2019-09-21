using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Pulse : MonoBehaviour {

	private static List<Color> RandomColors = new List<Color>(){Color.magenta, Color.cyan, Color.yellow, Color.red, Color.green, Color.blue};

	public SpriteRenderer sprite;

	public event Action<Pulse> OnKill;
	public event Action<Pulse> OnEarly;
	public event Action<Pulse> OnLate;
	public event Action<Pulse> OnStartPump;

	private Note note;
	private Vector3 origin;
	private Vector3 destination;
	private bool hasPumpStarted = false;
	private bool isPumpable = false;
	private bool isBeingPumped = false;
	private float? lastPumpLevel = null;
	private float score = 0;

	public Note Note { get { return this.note;}}
	public bool IsBeingPumped { get { return this.isBeingPumped; }}

	public void Init(Note note, Vector3 origin, Vector3 destination) {
		this.note = note;
		this.origin = origin;
		this.destination = destination;

		float xScale = Config.PULSE_UNITS_PER_VOLUME * note.volume * 0.8f;
		float yScale = (destination.y - origin.y) / Config.PULSE_TRAVEL_TIME_UP * note.length;

		this.transform.position = origin;
		this.sprite.transform.localScale = new Vector3(xScale, yScale, 1);
		this.sprite.transform.localPosition = new Vector3(0, -yScale/2, 0);
	}

	public void KillMaybe(float time) {
		if (time > this.note.time + this.note.length) {
			this.Kill();
		}
	}

	public void Move(float time) {
		this.transform.position = Vector3.LerpUnclamped(this.destination, this.origin, this.note.time - time);
	}

	public void Pump(float time, float timeDelta, float pumpLevel) {
		if (this.lastPumpLevel == null) {
			this.lastPumpLevel = pumpLevel;
		}

		bool onScreen = this.transform.position.y > this.origin.y;
		if (!this.hasPumpStarted && onScreen && pumpLevel > this.lastPumpLevel) {
			this.hasPumpStarted = true;

			if (this.OnStartPump != null) {
				this.OnStartPump.Invoke(this);
			}

			// early?
			if (this.note.time - time > Config.PULSE_PUMP_WINDOW) {
				if (this.OnEarly != null) {
					this.OnEarly(this);
				}
				this.score = -1 * (this.note.time - time) * Config.POINTS_PER_SECOND;
			}

			// late?
			if (time - this.note.time > Config.PULSE_PUMP_WINDOW) {
				if (this.OnLate != null) {
					this.OnLate(this);
				}
			}
		}

		this.isPumpable = time >= this.note.time && time <= this.note.time + this.note.length;

		if (this.isPumpable && this.hasPumpStarted) {
			if (pumpLevel > 0.01) {
				// visuals
				if (!this.isBeingPumped) {
					this.sprite.transform.DOScale(this.sprite.transform.localScale * 1.2f, 0.2f).SetEase(Ease.InOutQuad).SetLoops(2, LoopType.Yoyo);
					this.sprite.color = this.note.color;
				}
				this.isBeingPumped = true;

				// score
				this.score += timeDelta * Config.POINTS_PER_SECOND;

			} else {
				// visuals
				if (this.isBeingPumped) {
					this.sprite.color = Color.white;
				}
				this.isBeingPumped = false;
			}
		} 

		this.lastPumpLevel = pumpLevel;
	}

	private void Kill() {
		if (this.OnKill != null) {
			this.OnKill(this);
		}
		GameObject.Destroy(this.gameObject);

		if (this.score > 0) {
			GameController.Instance.Score.AddToScore(Mathf.RoundToInt(this.score));
		}
	}
}

﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

public class EditorInputProcessor
{
	List<KeyCode> keyCodes = new List<KeyCode>();
	List<int> mouseButtons = new List<int>();
	public enum State { Press, Release, While, All = Press | While | Release }
	List<StateAction> actions = new List<StateAction>();
	private bool checkAlt;
	private bool checkActionKey;
	private bool checkShift;
	private bool onPress;
	private bool onRelease;
	private bool onWhile;
	private bool keyPressed;
	private bool keyReleased;
	private bool keyPressedFirst;
	private float maxHoldTime;
	private DateTime keyPressDateTime;
	private Action onCancel;
	private Func<EditorInputProcessor, bool> cancelConditon;
	private bool maxHoldTimeout => maxHoldTime > 0 ? keyPressDateTime.AddSeconds(maxHoldTime) < DateTime.Now : false;

	public EditorInputProcessor OnButton(KeyCode key)
	{
		keyCodes.Add(key);
		return this;
	}
	public EditorInputProcessor OnMouse(int mouseButton)
	{
		mouseButtons.Add(mouseButton);
		return this;
	}
	public EditorInputProcessor OnModifier(bool alt = false, bool actionKey = false, bool shift = false)
	{
		checkAlt = alt;
		checkActionKey = actionKey;
		checkShift = shift;
		return this;
	}
	public EditorInputProcessor OnPress(Action action)
	{
		onPress = true;
		actions.Add(new StateAction() { state = State.Press, action = action });
		return this;
	}
	public EditorInputProcessor OnRelease(Action action)
	{
		onRelease = true;
		actions.Add(new StateAction() { state = State.Release, action = action });
		return this;
	}
	public EditorInputProcessor OnWhile(Action action)
	{
		onWhile = true;
		actions.Add(new StateAction() { state = State.While, action = action });
		return this;
	}
	public EditorInputProcessor OnCancel(Action action)
	{
		onCancel = action;
		return this;
	}
	public EditorInputProcessor MaxHold(float time)
	{
		maxHoldTime = time;
		return this;
	}
	internal EditorInputProcessor CancelCondition(Func<EditorInputProcessor, bool> cond)
	{
		cancelConditon = cond;
		return this;
	}


	class StateAction
	{
		public Action action;
		public State state;
	}


	public void ProcessEvent(Event current)
	{
		keyReleased = false;
		if (!onPress && !onRelease && !onWhile) return;
		if ((onPress || onWhile) && current.type == EventType.KeyDown && keyCodes.Contains(current.keyCode))
			keyPressedFirst = IsModifierPressedIfTracked(current);
		else if ((onRelease || onWhile) && current.type == EventType.KeyUp && keyCodes.Contains(current.keyCode))
		{
			keyPressed = false;
			keyReleased = true;
		}
		else if ((onPress || onWhile) && current.type == EventType.MouseDown && mouseButtons.Contains(current.button))
		{
			keyPressedFirst = IsModifierPressedIfTracked(current);
			if (maxHoldTime > 0) keyPressDateTime = DateTime.Now;
		}
		else if ((onRelease || onWhile) && current.type == EventType.MouseUp && mouseButtons.Contains(current.button))
		{
			keyPressed = false;
			keyReleased = true;
		}
		if (!(keyPressed | keyReleased | keyPressedFirst)) return;

		if (onWhile && keyPressed)
		{
			if (maxHoldTimeout || (cancelConditon?.Invoke(this) ?? false))
			{
				Cancel();
				return;
			}
			actions.Where(a=>a.state == State.While).Foreach(a=>a.action());
			keyPressedFirst = false;
			return;
		}
		else if (onPress && keyPressedFirst)
		{
			actions.Where(a=>a.state == State.Press).Foreach(a=>a.action());
			keyPressed = onWhile;
			keyPressedFirst = false;
			return;
		}
		else if (onRelease && keyReleased)
		{
			if (maxHoldTimeout || (cancelConditon?.Invoke(this) ?? false))
			{
				Cancel();
				return;
			}
			actions.Where(a=>a.state == State.Release).Foreach(a=>a.action());
			return;
		}

	}

	public bool IsPressed => keyPressedFirst;
	public bool IsHeld => keyPressed;
	public bool IsReleased => keyReleased;

	private bool IsModifierPressedIfTracked(Event current) =>
		!checkAlt && !checkShift && !checkActionKey ? true :
		checkShift && current.shift ? true :
		checkActionKey && EditorGUI.actionKey ? true :
		checkAlt && current.alt ? true : false;

	internal void Cancel()
	{
		keyPressedFirst = false;
		keyPressed = false;
		keyReleased = false;
		onCancel?.Invoke();
	}
}
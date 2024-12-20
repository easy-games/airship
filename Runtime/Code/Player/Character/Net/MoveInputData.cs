﻿using System;
using Assets.Luau;
using UnityEngine;

/// <summary>
/// MoveInputData is the movement command stream that keeps track of what the
/// player wants to do. For example, move in a specific direction, or jump.
///
/// TS/Luau can use the CustomData interface to write arbitrary data to this stream.
/// </summary>
public struct MoveInputData : IEquatable<MoveInputData>{
	public Vector3 moveDir;
	public bool jump;
	public bool crouch;
	public bool sprint;
	public Vector3 lookVector;
	public BinaryBlob customData;

	public MoveInputData(Vector3 moveDir, bool jump, bool crouch, bool sprint, Vector3 lookVector, BinaryBlob customData) {
		this.moveDir = moveDir;
		this.jump = jump;
		this.crouch = crouch;
		this.sprint = sprint;
		this.lookVector = lookVector;
		this.customData = customData;
	}

    public bool Equals(MoveInputData other) {
		return moveDir == other.moveDir &&
			jump == other.jump && 
			crouch == other.crouch &&
			sprint == other.sprint &&
			((customData == null && other.customData == null) ||
				(customData != null && customData.Equals(other.customData)));//&& 
			//lookVector == other.lookVector;
    }

    // override object.GetHashCode
    public override readonly int GetHashCode() {
		return (moveDir, jump, crouch, sprint, lookVector, customData).GetHashCode();
	}
}

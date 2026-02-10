using System;
using UnityEngine;

/// <summary>
/// Represents a slice of a string without copying the underlying string.
/// </summary>
public struct StringSlice : IEquatable<StringSlice> {
	public readonly string Original;
	public int Length { get; private set; }

	private int _start;

	public StringSlice(string str) {
		Original = str;
		Length = str.Length;
		_start = 0;
	}

	public StringSlice(string str, int start) {
		start = Mathf.Clamp(start, 0, str.Length - 1);
		Original = str;
		Length = str.Length - start;
		_start = start;
	}

	public StringSlice(string str, int start, int len) {
		start = Mathf.Clamp(start, 0, str.Length - 1);
		Original = str;
		Length = Mathf.Min(str.Length - start, len);
		_start = start;
	}

	public StringSlice Substring(int start) {
		return new StringSlice(Original, _start + start);
	}

	public StringSlice Substring(int start, int len) {
		return new StringSlice(Original, _start + start, len);
	}

	public int LastIndexOf(char c) {
		return Original.LastIndexOf(c, _start, Length);
	}

	public int LastIndexOf(char c, int startIndex) {
		return Original.LastIndexOf(c, _start + startIndex, Length - startIndex);
	}

	public bool StartsWith(string other) {
		var aLen = Length;
		var bLen = other.Length;

		var ap = 0;
		var bp = 0;

		while (ap < aLen && bp < bLen && this[ap] == other[bp]) {
			ap++;
			bp++;
		}

		return bp == bLen;
	}

	public bool StartsWithIgnoreCase(string other) {
		var aLen = Length;
		var bLen = other.Length;

		var ap = 0;
		var bp = 0;

		while (ap < aLen && bp < bLen && char.ToUpperInvariant(this[ap]) == char.ToUpperInvariant(other[bp])) {
			ap++;
			bp++;
		}

		return bp == bLen;
	}

	private bool EndsWith(string other) {
		var ap = Length - 1;
		var bp = other.Length - 1;

		while (ap >= 0 && bp >= 0 && this[ap] == other[bp]) {
			ap--;
			bp--;
		}

		return bp < 0;
	}

	private bool EndsWithIgnoreCase(string other) {
		var ap = Length - 1;
		var bp = other.Length - 1;

		while (ap >= 0 && bp >= 0 && char.ToUpperInvariant(this[ap]) == char.ToUpperInvariant(other[bp])) {
			ap--;
			bp--;
		}

		return bp < 0;
	}

	public char this[int i] => Original[i + _start];

	public override string ToString() {
		if (_start == 0 && Length == Original.Length) {
			return Original;
		}

		return Original.Substring(_start, Length);
	}

	public bool Equals(StringSlice other) {
		if (Length != other.Length) return false;

		var span = Original.AsSpan(_start, Length);
		var otherSpan = other.Original.AsSpan(other._start, other.Length);
		
		return span.SequenceEqual(otherSpan);
	}
	
	public override bool Equals(object obj) {
		return obj is StringSlice other && Equals(other);
	}

	public override int GetHashCode() {
		return HashCode.Combine(Original, _start, Length);
	}
}

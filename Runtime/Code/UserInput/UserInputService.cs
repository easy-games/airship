using System;
using FishNet;
using UnityEngine;
#pragma warning disable CS0414

[LuauAPI]
public class UserInputService {
	private static bool _init = false;
	private static string _controlScheme = null;

	public static void SetInputProxy(InputProxy inputProxy)
	{
		InputProxy = inputProxy;
	}

	public static InputProxy InputProxy;
}


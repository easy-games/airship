using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;


namespace Code.Player.Character {
    [LuauAPI]
    public class AnimationClipOptions{
        public float fadeDuration = .125f;
        public FadeMode fadeMode = FadeMode.FixedSpeed;
        public bool autoFadeOut = true;
        public float playSpeed = 1;
        public AnimationClip fadeOutToClip = null;
    }
}

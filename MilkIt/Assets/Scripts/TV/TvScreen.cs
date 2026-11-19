using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace MilkIt.TV
{
    [RequireComponent(typeof(VideoPlayer), typeof(AudioSource))]
    public class TvScreen : MonoBehaviour
    {
        [Tooltip("Renderer that shows the picture. Give it an unlit material (URP > Unlit).")]
        public Renderer screen;
        [Tooltip("Picture resolution. Clips are scaled to fit inside it, with black bars if needed.")]
        public Vector2Int resolution = new Vector2Int(854, 480);
        [Tooltip("Played in this order; when a clip ends the next one starts, wrapping around to the first.")]
        public List<VideoClip> clips = new List<VideoClip>();

        VideoPlayer player;
        AudioSource audioSource;
        RenderTexture picture;
        Material screenMaterial;
        bool isOn;
        int currentClip;

        void Awake()
        {
            player = GetComponent<VideoPlayer>();
            audioSource = GetComponent<AudioSource>();
            picture = new RenderTexture(resolution.x, resolution.y, 0);

            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = picture;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, audioSource);
            player.loopPointReached += _ => NextClip();

            screenMaterial = screen.material;
            screenMaterial.SetTexture("_BaseMap", picture);
            Blank();
        }

        void OnDestroy()
        {
            Destroy(screenMaterial);
            Destroy(picture);
        }

        public void TogglePower()
        {
            if (isOn) TurnOff();
            else TurnOn();
        }

        public void TurnOn()
        {
            if (isOn || clips.Count == 0) return;
            isOn = true;
            PlayClip(Random.Range(0, clips.Count));
        }

        public void TurnOff()
        {
            if (!isOn) return;
            isOn = false;
            player.Stop();
            Blank();
        }

        public void NextClip()
        {
            if (isOn) PlayClip((currentClip + 1) % clips.Count);
        }

        public void ToggleMute()
        {
            audioSource.mute = !audioSource.mute;
        }

        void PlayClip(int index)
        {
            currentClip = index;
            Blank();
            player.clip = clips[index];
            player.Play();
        }

        void Blank()
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = picture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = previous;
        }
    }
}

using UnityEngine;

namespace Loongdum.SceneFlow
{
    /// <summary>
    /// 整局一首的 BGM。
    ///
    /// 挂在场景流那个常驻对象（GameSceneManager 所在的 "Scene Flow" 根对象）上，而那个对象是
    /// DontDestroyOnLoad 的，所以切关时音乐不会重头开始。素材槽位留空就静默不放，不报错 ——
    /// 和 AudioOneShot 的处理一致。
    /// </summary>
    public sealed class BackgroundMusic : MonoBehaviour
    {
        private AudioSource source;

        /// <summary>在 <paramref name="host"/> 上取一个（没有就建一个）播放器，开始循环放 <paramref name="clip"/>。</summary>
        public static void Ensure(GameObject host, AudioClip clip, float volume)
        {
            if (host == null || clip == null)
                return;

            BackgroundMusic music = host.GetComponent<BackgroundMusic>();
            if (music == null)
                music = host.AddComponent<BackgroundMusic>();
            music.Play(clip, volume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                // BGM 不分远近，和 AudioOneShot 一样按 2D 播；总音量交给 AudioListener.volume。
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
            }

            source.volume = Mathf.Clamp01(volume);

            // Initialize 每次加载场景都会跑一遍，同一首正在放就别打断它。
            if (source.clip == clip && source.isPlaying)
                return;

            source.clip = clip;
            source.Play();
        }
    }
}

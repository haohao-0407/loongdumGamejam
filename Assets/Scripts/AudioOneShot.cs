using UnityEngine;

/// <summary>
/// 极简的一次性音效播放入口。
///
/// 本工程没有接入 Wwise，也没有任何既有的音频层，所以这里刻意只做最小的一件事：
/// 在目标物体上取一个（没有就建一个）AudioSource，PlayOneShot 播一声。
/// <see cref="AudioClip"/> 传空则直接静默返回 —— 这样在 Inspector 里把槽位清空
/// 就等于「关掉这个音」，不用改代码、也不会报错刷 Console。
/// </summary>
public static class AudioOneShot
{
    /// <summary>在 <paramref name="target"/> 上播一声 <paramref name="clip"/>；任一为空则什么都不做。</summary>
    public static void Play(AudioClip clip, GameObject target, float volume = 1f)
    {
        if (clip == null || target == null || volume <= 0f)
            return;

        AudioSource source = target.GetComponent<AudioSource>();
        if (source == null)
        {
            source = target.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            // 玩家自己的脚步和交互音不需要距离衰减，统一按 2D 播。
            // 以后要做 3D 衰减，把这里改成 spatialBlend = 1 并配好 rolloff 即可。
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}

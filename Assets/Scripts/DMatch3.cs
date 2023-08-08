using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;


[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public class DMatch3 : MonoBehaviour
{
	[SerializeField] private UnityEngine.Audio.AudioMixer m_audioMixer;
	[SerializeField] private Slider m_volumeSlider;
	[SerializeField] private string m_volumeParamName = "VolumeDB"; // TODO: split param names for prefs/slider since one is a percentage and the other in decibels?

	[SerializeField] private MatchGrid m_gridPrefab;


	private MatchGrid m_gridCurrent;
	private bool m_modeCurrent;

	private class TextureInfo
	{
		public TextureInfo(string filepath) { m_filepath = filepath; }
		public readonly string m_filepath;
		public List<UniGif.GifTexture> m_textures = null;
		public int m_width;
		public int m_height;
	}
	private readonly List<TextureInfo> m_loadedTextures = new();


	private void Start()
	{
		if (PlayerPrefs.HasKey(m_volumeParamName))
		{
			// NOTE that setting m_slider.value will invoke SetVolume() via m_slider.onValueChanged
			m_volumeSlider.value = PlayerPrefs.GetFloat(m_volumeParamName);
		}
		else
		{
			// set volume in case the slider default doesn't correspond to the volume default
			SetVolume(m_volumeSlider.value);
		}

		Restart();
	}


	public IEnumerator GetOrLoadAnimatedTextures(string filepath, System.Action<List<UniGif.GifTexture>, int, int, int> callback)
	{
		// check already-loaded/loading entries
		TextureInfo entry = m_loadedTextures.Find(pair => pair.m_filepath == filepath);
		if (entry != null)
		{
			// wait if in-progress
			if (entry.m_textures == null)
			{
				yield return new WaitUntil(() => entry.m_textures != null);
			}
		}
		else
		{
			// add new entry first to prevent missing in-progress loads
			entry = new(filepath);
			m_loadedTextures.Add(entry);

			// load
			byte[] file_bytes = null;
			yield return FileReadAllBytes(filepath, returned_bytes => file_bytes = returned_bytes);
			yield return UniGif.GetTextureListCoroutine(file_bytes, (List<UniGif.GifTexture> textureList, int loopCount, int width, int height) =>
			{
				entry.m_textures = textureList;
				entry.m_width = width;
				entry.m_height = height;
			}, FilterMode.Point);
		}

		// notify
		callback(entry.m_textures, 0, entry.m_width, entry.m_height);
	}

	public void Restart()
	{
		if (m_gridCurrent != null)
		{
			Destroy(m_gridCurrent.gameObject);
		}
		m_gridCurrent = Instantiate(m_gridPrefab, gameObject.transform);
		m_gridCurrent.Init(this, m_modeCurrent);
	}

	public void ModeSwap()
	{
		m_modeCurrent = !m_modeCurrent;
		Restart();
	}

	public void SetVolume(float pctRaw)
	{
		m_audioMixer.SetFloat(m_volumeParamName, PercentToDecibels(pctRaw));
		PlayerPrefs.SetFloat(m_volumeParamName, pctRaw);
	}


	private IEnumerator FileReadAllBytes(string filename, System.Action<byte[]> callback)
	{
		// NOTE that we could use System.IO for non-web builds, but since WebRequest also works for local files, we just stick to it always for simplicity and ease of testing
		string finalPath = Application.streamingAssetsPath + System.IO.Path.DirectorySeparatorChar + filename;
		Debug.Log("Reading file " + finalPath);
		using UnityWebRequest request = UnityWebRequest.Get(finalPath);
		yield return request.SendWebRequest();
		callback(request.downloadHandler.data);
	}

	private float PercentToDecibels(float pct) => Mathf.Log10(pct) * 20.0f; // for formula source, see https://johnleonardfrench.com/the-right-way-to-make-a-volume-slider-in-unity-using-logarithmic-conversion/
}

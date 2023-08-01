using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public static class ExtensionHelpers
{
	public static bool All<T>(this T[,] source, System.Func<T, bool> predicate)
	{
		for (int i = 0, n = source.GetLength(0); i < n; ++i)
		{
			for (int j = 0, m = source.GetLength(1); j < m; ++j)
			{
				if (!predicate(source[i, j]))
				{
					return false;
				}
			}
		}
		return true;
	}
};


public class MatchGrid : MonoBehaviour
{
	const int m_matchLen = 3;

	[SerializeField] private Sprite[] m_sprites;
	[SerializeField] private GridSlot m_slotPrefab;
	[SerializeField] private int m_spritesMin = 3; // TODO: base on grid size?
	[SerializeField] private int m_spritesMax = 5;
	[SerializeField] private TMPro.TMP_Text m_scoreText;
	[SerializeField] private float m_padding = 0.0f;
	[SerializeField] private float m_recursiveMatchDelay = 0.5f;

	private int m_width = 3;
	private int m_height = 3;

	private string[] m_spriteFilepathsCurrent;
	private Sprite[] m_spritesCurrent;

	private float m_slotWidth;
	private float m_slotHeight;
	private Vector3 m_cornerPos; // TODO: don't assume the (local) position will never change?

	private DMatch3 m_game;
	private bool m_mode;
	private GridSlot[,] m_slots;
	private bool m_isProcessing = false;
	private int m_score = 0;
	private int m_lastSfxFrame = -1;


	public void Init(DMatch3 game, bool mode, int width, int height)
	{
		Debug.Assert(m_game == null && m_slots == null && width >= 3 && height >= 3);
		m_game = game;
		m_mode = mode;
		m_width = width;
		m_height = height;
	}


	private void Start()
	{
		int spriteCount = Random.Range(m_spritesMin, m_spritesMax);
		if (m_mode)
		{
			m_spritesCurrent = m_sprites.OrderBy(s => Random.value).Take(spriteCount).ToArray();
		}
		else
		{
			// TODO: avoid re-getting w/ each new grid?
			m_spriteFilepathsCurrent = System.IO.Directory.GetFiles(Application.streamingAssetsPath, "*.gif").OrderBy(i => Random.value).Take(spriteCount).ToArray();
		}

		RectTransform tf = m_slotPrefab.GetComponent<RectTransform>();
		Rect rect = tf.rect;
		m_slotWidth = rect.width + m_padding;
		m_slotHeight = rect.height + m_padding;
		float totalWidth = m_slotWidth * m_width;
		float totalHeight = m_slotHeight * m_height;
		m_cornerPos = gameObject.transform.localPosition + new Vector3((m_slotWidth - totalWidth) * 0.5f, (m_slotHeight - totalHeight) * 0.5f);

		m_slots = new GridSlot[m_width, m_height];
		for (int i = 0; i < m_width; i++)
		{
			for (int j = 0; j < m_height; j++)
			{
				AddNewInSlot(i, j);
			}
		}

		StartCoroutine(DelayedProcessMatches());
	}

	public IEnumerator GetOrLoadAnimatedTextures(string filepath, System.Action<List<UniGif.GifTexture>, int, int, int> callback)
	{
		yield return m_game.GetOrLoadAnimatedTextures(filepath, callback);
	}

	public GridSlot SlotAtPosition(Vector3 position)
	{
		Vector2Int coord = CoordForPosition(position);
		return IsValidCoord(coord) ? m_slots[coord.x, coord.y] : null;
	}

	public void Swap(Vector3 homePos, Vector2 diff)
	{
		if (!m_isProcessing)
		{
			StartCoroutine(SwapInternal(CoordForPosition(homePos), Mathf.Abs(diff.x) >= Mathf.Abs(diff.y) ? new Vector2Int((int)Mathf.Sign(diff.x), 0) : new Vector2Int(0, (int)Mathf.Sign(diff.y)), true));
		}
	}

	public bool ShouldPlaySfxThisFrame()
	{
		if (m_lastSfxFrame != Time.frameCount)
		{
			m_lastSfxFrame = Time.frameCount;
			return true;
		}
		return false;
	}


	private IEnumerator SwapInternal(Vector2Int coordStart, Vector2Int coordDiff, bool smooth)
	{
		Vector2Int coordEnd = coordStart + coordDiff;

		if (!IsValidCoord(coordEnd))
		{
			yield break;
		}

		// notify slots
		List<Coroutine> coroutines = new();
		if (m_slots[coordStart.x, coordStart.y] != null)
		{
			coroutines.Add(m_slots[coordStart.x, coordStart.y].SetHomePosition(PositionForCoord(coordEnd.x, coordEnd.y), smooth));
		}
		if (m_slots[coordEnd.x, coordEnd.y] != null)
		{
			coroutines.Add(m_slots[coordEnd.x, coordEnd.y].SetHomePosition(PositionForCoord(coordStart.x, coordStart.y), smooth));
		}

		// swap internally
		(m_slots[coordEnd.x, coordEnd.y], m_slots[coordStart.x, coordStart.y]) = (m_slots[coordStart.x, coordStart.y], m_slots[coordEnd.x, coordEnd.y]);

		foreach (Coroutine c in coroutines)
		{
			yield return c;
		}

		if (smooth) // NOTE that this corresponds to when triggered by the player rather than by recursive matches; ProcessMatches() contains a delayed recursive invocation that takes care of that
		{
			StartCoroutine(ProcessMatches());
		}
	}

	private Vector2Int CoordForPosition(Vector3 position)
	{
		Vector3 offset = position - m_cornerPos;
		return new Vector2Int((int)(offset.x / m_slotWidth), (int)(offset.y / m_slotHeight));
	}

	private Vector3 PositionForCoord(int x, int y) => m_cornerPos + new Vector3(x * m_slotWidth, y * m_slotHeight);

	private bool IsValidCoord(Vector2Int coord) => coord.x >= 0 && coord.y >= 0 && coord.x < m_width && coord.y < m_height;

	private void AddNewInSlot(int x, int y)
	{
		Debug.Assert(m_slots[x, y] == null);
		GridSlot newSlot = Instantiate(m_slotPrefab, gameObject.transform);
		newSlot.transform.localPosition = PositionForCoord(x, y);
		newSlot.m_spriteFilepaths = m_spriteFilepathsCurrent;
		newSlot.m_sprites = m_spritesCurrent;
		m_slots[x, y] = newSlot;
	}

	private IEnumerator DelayedProcessMatches()
	{
		m_isProcessing = true;
		yield return new WaitUntil(() => m_slots.All(slot => slot.ImagesLoaded && !slot.IsLerping));
		yield return ProcessMatches();
		m_isProcessing = false;
	}

	private IEnumerator ProcessMatches()
	{
		m_isProcessing = true;

		// check for matches
		// TODO: detect extra-long and intersecting matches
		List<Vector2Int> slotsToRemove = new();
		for (int i = 0; i < m_width; ++i)
		{
			for (int j = 0; j < m_height; ++j)
			{
				Debug.Assert(m_slots[i, j] != null);

				// iterate horizontally/vertically
				int matchCountH = 1;
				int matchCountV = 1;
				while (i + matchCountH < m_width && m_slots[i, j].Matches(m_slots[i + matchCountH, j]))
				{
					++matchCountH;
				}
				while (j + matchCountV < m_height && m_slots[i, j].Matches(m_slots[i, j + matchCountV]))
				{
					++matchCountV;
				}

				// mark for removal
				if (matchCountH >= m_matchLen)
				{
					for (int a = 0; a < matchCountH; ++a)
					{
						slotsToRemove.Add(new Vector2Int(i + a, j));
					}
				}
				if (matchCountV >= m_matchLen)
				{
					for (int b = 0; b < matchCountV; ++b)
					{
						slotsToRemove.Add(new Vector2Int(i, j + b));
					}
				}
			}
		}

		// remove matching slots
		foreach (Vector2Int coord in slotsToRemove)
		{
			if (m_slots[coord.x, coord.y] == null)
			{
				// TODO: this slot must've been involved in intersecting matches; replace w/ special slot item?
			}
			else
			{
				m_slots[coord.x, coord.y].StartDespawn();
				m_slots[coord.x, coord.y] = null;
			}
		}

		// handle removed slots
		if (slotsToRemove.Count > 0)
		{
			// increment score
			m_score += slotsToRemove.Count; // NOTE that this purposely counts slots multiple times if they are involved in multiple matches
			m_scoreText.text = m_score.ToString();

			// trigger "falling"
			List<Coroutine> coroutines = new();
			for (int i = 0; i < m_width; ++i)
			{
				int numHoles = 0;
				for (int j = 0; j < m_height; ++j)
				{
					if (m_slots[i, j] == null)
					{
						++numHoles;
					}
					else if (numHoles > 0)
					{
						coroutines.Add(StartCoroutine(SwapInternal(new Vector2Int(i, j), new Vector2Int(0, -numHoles), false)));
					}
				}
				for (int k = 1; k <= numHoles; ++k)
				{
					AddNewInSlot(i, m_height - k);
				}
			}

			// once done, delay and check for more matches
			foreach (Coroutine c in coroutines)
			{
				yield return c;
			}
			yield return new WaitForSeconds(m_recursiveMatchDelay);
			yield return StartCoroutine(ProcessMatches());
		}

		m_isProcessing = false;
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[RequireComponent(typeof(RawImage), typeof(AudioSource))]
public class GridSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	public string[] m_spriteFilepaths { private get; set; }
	public Sprite[] m_sprites { private get; set; }

	[SerializeField] private AudioClip[] m_bounceSfx;

	[SerializeField] private float m_bounceScalarBase = 0.06f;
	[SerializeField] private float m_bounceScalarVariance = 0.01f;

	[SerializeField] private float m_lerpEpsilon = 1.0f;
	[SerializeField] private float m_lerpTimePerDistance = 0.001f;
	private float m_lerpEpsilonSq;
	[SerializeField] private float m_despawnAccel = 0.2f;


	public bool IsLerping { get; private set; }
	public bool ImagesLoaded { get; private set; }


	private AudioSource m_audioSource;
	private MatchGrid m_grid;
	private Vector2 m_size;
	private int m_spriteIdx;

	private Vector3 m_homePos;
	private Vector3 m_dragStartPos;
	private int m_bounceCount;


	private void Start()
	{
		m_lerpEpsilonSq = m_lerpEpsilon * m_lerpEpsilon;

		m_grid = GetComponentInParent<MatchGrid>();
		m_audioSource = GetComponent<AudioSource>();

		m_size = GetComponent<RectTransform>().rect.size;
		Vector3 posOrig = transform.localPosition;
		transform.localPosition += new Vector3(0.0f, Screen.height);
		SetHomePosition(posOrig, false);

		RawImage image = GetComponent<RawImage>();
		if (m_spriteFilepaths != null && m_spriteFilepaths.Length > 0) // TODO: support mix-and-match?
		{
			m_spriteIdx = Random.Range(0, m_spriteFilepaths.Length);
			StartCoroutine(Animate(image, m_spriteFilepaths[m_spriteIdx]));
		}
		else
		{
			Debug.Assert(m_sprites != null && m_sprites.Length > 0);
			m_spriteIdx = Random.Range(0, m_sprites.Length);
			image.texture = m_sprites[m_spriteIdx].texture; // TODO: support spritesheets?

			// scale to avoid overlap
			// TODO: helper function?
			Vector2 newSize = new(image.texture.width, image.texture.height);
			if (newSize.x > m_size.x || newSize.y > m_size.y)
			{
				Vector2 scale = m_size / newSize;
				newSize *= Mathf.Min(scale.x, scale.y);
			}
			image.GetComponent<RectTransform>().sizeDelta = newSize;

			ImagesLoaded = true;
		}
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		// TODO: restart animation? grow?
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		// TODO: pause animation? shrink?
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		m_dragStartPos = (Vector3)eventData.position;
		transform.SetAsLastSibling(); // to ensure rendering on top
	}

	public void OnDrag(PointerEventData eventData)
	{
		Vector3 diff = (Vector3)eventData.position - m_dragStartPos;
		if (Mathf.Abs(diff.x) > m_size.x || Mathf.Abs(diff.y) > m_size.y)
		{
			eventData.pointerDrag = null;
			m_grid.Swap(m_homePos, diff);
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		StartCoroutine(LerpHome(true));
	}

	public void SwapWith(GridSlot replaceSlot)
	{
		Vector3 temp = m_homePos;
		SetHomePosition(replaceSlot.m_homePos, true);
		replaceSlot.SetHomePosition(temp, true);
	}

	public bool Matches(GridSlot other) => other != null && m_spriteIdx == other.m_spriteIdx;

	public Coroutine SetHomePosition(Vector3 position, bool smooth)
	{
		m_homePos = position;
		return StartCoroutine(LerpHome(smooth));
	}

	public void StartDespawn() => StartCoroutine(Despawn());


	private IEnumerator LerpHome(bool smooth)
	{
		Debug.Assert(smooth || (Mathf.Approximately(transform.localPosition.x, m_homePos.x) && Mathf.Approximately(transform.localPosition.z, m_homePos.z)), "Non-smooth lerping must be purely vertical.");
		if (IsLerping)
		{
			yield break;
		}
		IsLerping = true;

		if (!ImagesLoaded)
		{
			yield return new WaitUntil(() => ImagesLoaded);
		}

		m_bounceCount = 0;
		Vector3 vel = Vector3.zero; // TODO: estimate release velocity?
		float lerpTime = m_lerpTimePerDistance * (m_homePos - transform.localPosition).magnitude;

		while ((transform.localPosition - m_homePos).sqrMagnitude > m_lerpEpsilonSq || vel.magnitude > m_lerpEpsilon)
		{
			if (smooth)
			{
				transform.localPosition = new(Mathf.SmoothDamp(transform.localPosition.x, m_homePos.x, ref vel.x, lerpTime), Mathf.SmoothDamp(transform.localPosition.y, m_homePos.y, ref vel.y, lerpTime));
			}
			else
			{
				vel += (transform.localPosition.y > m_homePos.y) ? (Vector3)Physics2D.gravity : -(Vector3)Physics2D.gravity;
				transform.localPosition += vel * Time.deltaTime; // TODO: fixed timestep
				if (transform.localPosition.y <= m_homePos.y)
				{
					vel = m_bounceScalarBase * Random.Range(1.0f - m_bounceScalarVariance, 1.0f + m_bounceScalarVariance) * new Vector3(vel.x, Mathf.Abs(vel.y));

					if (m_grid.ShouldPlaySfxThisFrame())
					{
						m_audioSource.PlayOneShot(m_bounceSfx[Mathf.Min(m_bounceSfx.Length - 1, m_bounceCount)]);
					}

					++m_bounceCount;
					if (m_bounceCount >= m_bounceSfx.Length)
					{
						break;
					}
				}
			}

			yield return null;
		}
		transform.localPosition = m_homePos;

		IsLerping = false;
	}

	private IEnumerator Animate(RawImage image, string sprite_filepath)
	{
		// ensure we aren't visible while loading
		image.enabled = false;

		// load/set textures
		List<UniGif.GifTexture> textureList = null;
		yield return m_grid.GetOrLoadAnimatedTextures(sprite_filepath, (List<UniGif.GifTexture> textureListLoaded, int loopCount, int width, int height) =>
		{
			textureList = textureListLoaded;
			image.GetComponent<RectTransform>().sizeDelta = new(width, height); // TODO: clamp size via scaling if user-created sprites are ever allowed...
		});
		image.enabled = true;
		ImagesLoaded = true;

		// flip through texture list
		int i = 0;
		while (isActiveAndEnabled)
		{
			UniGif.GifTexture textureCur = textureList[i];
			image.texture = textureCur.m_texture2d;
			yield return new WaitForSeconds(textureCur.m_delaySec); // TODO: avoid re-creating waiter every time?
			i = (i + 1) % textureList.Count;
		}
	}

	private IEnumerator Despawn()
	{
		float vel = 0.0f;
		while (transform.localScale.x > 0.0f) // TODO: don't assume uniform scaling?
		{
			vel += m_despawnAccel * Time.deltaTime;
			float newScale = Mathf.Max(0.0f, transform.localScale.x - vel);
			transform.localScale = new(newScale, newScale, newScale);
			yield return null;
		}
		Destroy(gameObject);
	}
}

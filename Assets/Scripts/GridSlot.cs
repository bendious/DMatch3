using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[RequireComponent(typeof(RawImage))]
public class GridSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	public string[] m_spriteFilepaths { private get; set; }

	[SerializeField] private float m_bounceScalarBase = 0.5f;
	[SerializeField] private float m_bounceScalarVariance = 0.1f;

	[SerializeField] private float m_lerpEpsilon = 1.0f;
	[SerializeField] private float m_lerpTimePerDistance = 0.001f;
	private float m_lerpEpsilonSq;
	[SerializeField] private float m_despawnAccel = 0.2f;


	public bool IsLerping { get; private set; }
	public bool ImagesLoaded { get; private set; }


	private MatchGrid m_grid;
	private Vector2 m_size;
	private int m_spriteIdx;

	private Vector3 m_homePos;
	private Vector3 m_dragStartPos;

	private void Start()
	{
		m_lerpEpsilonSq = m_lerpEpsilon * m_lerpEpsilon;

		m_grid = GetComponentInParent<MatchGrid>();

		m_size = GetComponent<RectTransform>().rect.size;
		Vector3 posOrig = transform.position;
		transform.position += new Vector3(0.0f, Screen.height);
		SetHomePosition(posOrig, false);

		m_spriteIdx = Random.Range(0, m_spriteFilepaths.Length);
		RawImage image = GetComponent<RawImage>();
		StartCoroutine(Animate(image, m_spriteFilepaths[m_spriteIdx]));
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
		Debug.Assert(smooth || (transform.position.x == m_homePos.x && transform.position.z == m_homePos.z), "Non-smooth lerping must be purely vertical.");
		if (IsLerping)
		{
			yield break;
		}
		IsLerping = true;

		if (!ImagesLoaded)
		{
			yield return new WaitUntil(() => ImagesLoaded);
		}

		Vector3 vel = Vector3.zero; // TODO: estimate release velocity?
		float lerpTime = m_lerpTimePerDistance * (m_homePos - transform.position).magnitude;

		while ((transform.position - m_homePos).sqrMagnitude > m_lerpEpsilonSq || vel.magnitude > m_lerpEpsilon)
		{
			if (smooth)
			{
				transform.position = new(Mathf.SmoothDamp(transform.position.x, m_homePos.x, ref vel.x, lerpTime), Mathf.SmoothDamp(transform.position.y, m_homePos.y, ref vel.y, lerpTime));
			}
			else
			{
				vel += (transform.position.y > m_homePos.y) ? (Vector3)Physics2D.gravity : -(Vector3)Physics2D.gravity;
				transform.position += vel * Time.deltaTime; // TODO: fixed timestep?
				if (transform.position.y <= m_homePos.y)
				{
					vel = m_bounceScalarBase * Random.Range(1.0f - m_bounceScalarVariance, 1.0f + m_bounceScalarVariance) * new Vector3(vel.x, Mathf.Abs(vel.y));
					// TODO: bounce SFX
				}
			}

			yield return null;
		}
		transform.position = m_homePos;

		IsLerping = false;
	}

	private IEnumerator Animate(RawImage image, string sprite_filepath)
	{
		// TODO: avoid re-loading w/ each new GridSlot
		List<UniGif.GifTexture> textureList = null;
		yield return UniGif.GetTextureListCoroutine(System.IO.File.ReadAllBytes(sprite_filepath), (List<UniGif.GifTexture> textureListLoaded, int loopCount, int width, int height) =>
		{
			textureList = textureListLoaded;
			image.GetComponent<RectTransform>().sizeDelta = new(width, height);
		});
		ImagesLoaded = true;

		int i = 0;
		WaitForSeconds wait = new(1.0f / 24.0f); // TODO: read from .gif?
		while (isActiveAndEnabled)
		{
			image.texture = textureList[i].m_texture2d;
			i = (i + 1) % textureList.Count;
			yield return wait;
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

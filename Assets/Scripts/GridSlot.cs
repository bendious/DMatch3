using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[RequireComponent(typeof(Image))]
public class GridSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	[SerializeField] private Sprite[] m_sprites;

	[SerializeField] private float m_bounceScalarBase = 0.5f;
	[SerializeField] private float m_bounceScalarVariance = 0.1f;

	[SerializeField] private float m_lerpEpsilon = 1.0f;
	[SerializeField] private float m_lerpTimePerDistance = 0.001f;
	private float m_lerpEpsilonSq;
	[SerializeField] private float m_despawnAccel = 0.2f;


	private MatchGrid m_grid;
	private Vector2 m_size;
	private int m_spriteIdx;

	private Vector3 m_homePos;
	private Vector3 m_dragStartPos;
	private bool m_lerping = false;


	private void Start()
	{
		m_lerpEpsilonSq = m_lerpEpsilon * m_lerpEpsilon;

		m_grid = GetComponentInParent<MatchGrid>();

		m_size = GetComponent<RectTransform>().rect.size;
		Vector3 posOrig = transform.position;
		transform.position += new Vector3(0.0f, Screen.height);
		SetHomePosition(posOrig, false);

		m_spriteIdx = Random.Range(0, m_sprites.Length);
		GetComponent<Image>().sprite = m_sprites[m_spriteIdx];
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		// TODO: animate
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		// TODO: stop animating
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
		if (m_lerping)
		{
			yield break;
		}
		m_lerping = true;

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

		m_lerping = false;
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

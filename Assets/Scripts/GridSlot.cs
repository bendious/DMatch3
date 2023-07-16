using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


[RequireComponent(typeof(Image))]
public class GridSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	[SerializeField] private Sprite[] m_sprites;


	private const float m_lerpEpsilon = 1.0f;
	private const float m_lerpEpsilonSq = m_lerpEpsilon * m_lerpEpsilon;
	private const float m_lerpTimePerDistance = 0.001f;


	private MatchGrid m_grid;

	private Vector2 m_size;
	private Vector3 m_homePos;
	private Vector3 m_dragStartPos;
	private bool m_lerping = false;


	private void Start()
	{
		m_grid = GetComponentInParent<MatchGrid>();

		m_size = GetComponent<RectTransform>().rect.size;
		m_homePos = transform.position;

		GetComponent<Image>().sprite = m_sprites[Random.Range(0, m_sprites.Length)];
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
		StartCoroutine(LerpHome());
	}

	public void SwapWith(GridSlot replaceSlot)
	{
		Vector3 temp = m_homePos;
		SetHomePosition(replaceSlot.m_homePos);
		replaceSlot.SetHomePosition(temp);
	}

	private void SetHomePosition(Vector3 position)
	{
		m_homePos = position;
		StartCoroutine(LerpHome());
	}

	private IEnumerator LerpHome()
	{
		if (m_lerping)
		{
			yield break;
		}
		m_lerping = true;

		// TODO: estimate release velocity?
		float velX = 0.0f;
		float velY = 0.0f;
		float lerpTime = m_lerpTimePerDistance * (m_homePos - transform.position).magnitude;

		while ((transform.position - m_homePos).sqrMagnitude > m_lerpEpsilonSq)
		{
			transform.position = new(Mathf.SmoothDamp(transform.position.x, m_homePos.x, ref velX, lerpTime), Mathf.SmoothDamp(transform.position.y, m_homePos.y, ref velY, lerpTime));

			yield return null;
		}
		transform.position = m_homePos;

		m_lerping = false;
	}
}

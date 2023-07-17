using UnityEngine;


public class MatchGrid : MonoBehaviour
{
	[SerializeField] private GridSlot m_slotPrefab;
	[SerializeField] private float m_padding = 10.0f;

	private int m_width = 3;
	private int m_height = 3;

	private float m_slotWidth;
	private float m_slotHeight;
	private Vector3 m_cornerOffset;

	private GridSlot[][] m_slots;


	public void SetSize(int width, int height)
	{
		Debug.Assert(m_slots == null && width >= 3 && height >= 3);
		m_width = width;
		m_height = height;
	}


	private void Start()
	{
		RectTransform tf = m_slotPrefab.GetComponent<RectTransform>();
		Rect rect = tf.rect;
		m_slotWidth = rect.width + m_padding;
		m_slotHeight = rect.height + m_padding;
		float totalWidth = m_slotWidth * m_width;
		float totalHeight = m_slotHeight * m_height;
		m_cornerOffset = new Vector3(totalWidth * -0.5f, totalHeight * -0.5f);
		Vector3 cornerPos = gameObject.transform.position + m_cornerOffset;

		m_slots = new GridSlot[m_width][];
		for (int i = 0; i < m_width; i++)
		{
			m_slots[i] = new GridSlot[m_height];
			for(int j = 0; j < m_height; j++)
			{
				AddNewInSlot(i, j, cornerPos);
			}
		}
	}


	public GridSlot SlotAtPosition(Vector3 position)
	{
		Vector2Int coord = CoordForPosition(position);
		return IsValidCoord(coord) ? m_slots[coord.x][coord.y] : null;
	}

	public bool Swap(Vector3 homePos, Vector2 diff)
	{
		Vector2Int coordStart = CoordForPosition(homePos);
		Vector2Int coordDiff = Mathf.Abs(diff.x) >= Mathf.Abs(diff.y) ? new Vector2Int((int)Mathf.Sign(diff.x), 0) : new Vector2Int(0, (int)Mathf.Sign(diff.y));
		Vector2Int coordEnd = coordStart + coordDiff;

		if (!IsValidCoord(coordEnd))
		{
			return false;
		}

		// swap internally
		GridSlot tmp = m_slots[coordStart.x][coordStart.y];
		m_slots[coordStart.x][coordStart.y] = m_slots[coordEnd.x][coordEnd.y];
		m_slots[coordEnd.x][coordEnd.y] = tmp;

		// notify slots
		m_slots[coordStart.x][coordStart.y].SwapWith(m_slots[coordEnd.x][coordEnd.y]);

		// TODO: wait for animation
		// TODO: check for matches

		return true;
	}


	private Vector2Int CoordForPosition(Vector3 position)
	{
		Vector3 cornerPos = gameObject.transform.position + m_cornerOffset;
		Vector3 offset = position - cornerPos;
		return new Vector2Int((int)(offset.x / m_slotWidth), (int)(offset.y / m_slotHeight));
	}

	private bool IsValidCoord(Vector2Int coord) => coord.x >= 0 && coord.y >= 0 && coord.x < m_width && coord.y < m_height;

	private void AddNewInSlot(int x, int y, Vector3 cornerPos)
	{
		m_slots[x][y] = Instantiate(m_slotPrefab, cornerPos + new Vector3(x * m_slotWidth, y * m_slotHeight), gameObject.transform.rotation, gameObject.transform);
	}
}

using UnityEngine;


public class DMatch3 : MonoBehaviour
{
	[SerializeField] private MatchGrid m_gridPrefab;
	[SerializeField] private int m_gridSizeMin = 3;
	[SerializeField] private int m_gridSizeMax = 5;


	private MatchGrid m_gridCurrent;


	private void Start()
	{
		m_gridCurrent = Instantiate(m_gridPrefab);
		m_gridCurrent.SetSize(Random.Range(m_gridSizeMin, m_gridSizeMax + 1), Random.Range(m_gridSizeMin, m_gridSizeMax + 1));
	}
}

using UnityEngine;

public class Rotate : MonoBehaviour
{
	public float speed = 1;

	private void Update() {
		transform.Rotate(0, speed, 0);
	}
}

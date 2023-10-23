using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Simple2DCamera : MonoBehaviour {
	[Tooltip("The X coordinate where the camera should stop moving left. If all four constraints are 0, the camera will not be constrained")]
	public float minX = 0;
	[Tooltip("The X coordinate where the camera should stop moving right. If all four constraints are 0, the camera will not be constrained")]
	public float maxX = 0;
	[Tooltip("The Y coordinate where the camera should stop moving down. If all four constraints are 0, the camera will not be constrained")]
	public float minY = 0;
	[Tooltip("The Y coordinate where the camera should stop moving up. If all four constraints are 0, the camera will not be constrained")]
	public float maxY = 0;
	[Tooltip("How close the camera can zoom in")]
	public float minZoom = 1.0f;
	[Tooltip("How far the camera can zoom out")]
	public float maxZoom = 10.0f;
	[Tooltip("The amount of time the camera should take to go to the target")]
	public float damping = 8.0f;
	public float moveSpeed = 2.0f;
	public float zoomSpeed = 0.2f;

	private float posX = 0;
	private float posY = 0;
	private float zoom = 0;

	// Start is called before the first frame update
	void Start() {
		// XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX  GAME SPECIFIC CODE:  XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
		// Override camera boundaries with actual map boundaries:
		Vector3 cellSize = GameObject.Find("Grid").GetComponent<Grid>().cellSize;
		minX = 0;
		minY = 0;
		maxX = MapGen.mapWidth * cellSize.x;
		maxY = MapGen.mapHeight * cellSize.y;
		// XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

		// Camera starting position:
		posX = (minX + maxX) / 2;
		posY = (minY + maxY) / 2;
		zoom = GetComponent<Camera>().orthographicSize;
		transform.position = new Vector3(posX, posY, transform.position.z);
	}

	// Update is called once per frame
	void Update() {
		float actualMoveSpeed = moveSpeed * Time.deltaTime * zoom;
		// Reduce speed if the camera is moving diagonally:
		if (
			(Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.D)) ||
			(Input.GetKey(KeyCode.D) && Input.GetKey(KeyCode.S)) ||
			(Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.A)) ||
			(Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.W))
		) {
			actualMoveSpeed /= 1.41421356237f;
		}
		// Increase speed if shift is held:
		if (Input.GetKey(KeyCode.LeftShift)) {
			actualMoveSpeed *= 2;
		}

		bool hasConstraints = minX + maxX + minY + maxY != 0;

		// Move camera:
		if (Input.GetKey(KeyCode.W)) {
			posY = hasConstraints ? Math.Clamp(posY + actualMoveSpeed, minY, maxY) : posY + actualMoveSpeed;
		}
		if (Input.GetKey(KeyCode.A)) {
			posX = hasConstraints ? Math.Clamp(posX - actualMoveSpeed, minX, maxX) : posX - actualMoveSpeed;
		}
		if (Input.GetKey(KeyCode.S)) {
			posY = hasConstraints ? Math.Clamp(posY - actualMoveSpeed, minY, maxY) : posY - actualMoveSpeed;
		}
		if (Input.GetKey(KeyCode.D)) {
			posX = hasConstraints ? Math.Clamp(posX + actualMoveSpeed, minX, maxX) : posX + actualMoveSpeed;
		}
		// Zoom camera:
		float newZoom = zoom - Input.mouseScrollDelta.y * zoom * zoomSpeed;
		zoom = Math.Clamp(newZoom, minZoom, maxZoom);

		// Update camera position and zoom:
		Vector3 targetPosition = new Vector3(posX + 0.001f, posY + 0.001f, transform.position.z); // Add a small offset to prevent a bug where x/y values would glitch for target positions near 0
		if (!Equals(transform.position, targetPosition)) transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * damping);
		Camera cam = GetComponent<Camera>();
		if (cam.orthographicSize != zoom) cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, zoom, Time.deltaTime * damping);
	}
}

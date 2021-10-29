using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class H8DManager : MonoBehaviour
{
	public GameObject catalogerCanvas;
	public GameObject imagerCanvas;

	public bool mouseButtonDown;
	public bool shifted;

	public static H8DManager Instance;

    void Awake()
    {
		Instance = this;
    }

    void Start()
	{
		ActivateCataloger();
	}

	void Update()
	{
		if (Input.GetMouseButtonDown(0) || Input.GetButtonDown("Fire1"))
		{
			mouseButtonDown = true;
		}
		if (Input.GetKey(KeyCode.LeftShift))
		{
			shifted = true;
		}
	}

	public bool GetButtonDown()
	{
		if (mouseButtonDown)
		{
			mouseButtonDown = false;
			return true;
		}
		return false;
	}

	public bool IsShifted()
	{
		if (shifted)
		{
			shifted = false;
			return true;
		}
		return false;
	}

	public void ActivateImager()
	{
		imagerCanvas.SetActive(true);
		catalogerCanvas.SetActive(false);
		H8DImager.Instance.COMInit();
	}

	public void ActivateCataloger()
	{
		imagerCanvas.SetActive(false);
		catalogerCanvas.SetActive(true);
	}
}

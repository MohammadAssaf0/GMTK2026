// by @torahhorse

// Instructions:
// Place on player. OnBelowLevel will get called if the player ever falls below

using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class CheckIfBelowLevel : MonoBehaviour
{
	public float resetBelowThisY = -100f;
	public bool fadeInOnReset = true;
	
	private Vector3 startingPosition;
	
	void Awake()
	{
		startingPosition = transform.position;
	}
	
	void Update ()
	{
		if( transform.position.y < resetBelowThisY )
		{
			OnBelowLevel();
		}
	}
	
	private void OnBelowLevel()
	{
		Debug.Log("Player fell below level");
	
		
		// alternatively, you could just reload the current scene using this line:
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}
}

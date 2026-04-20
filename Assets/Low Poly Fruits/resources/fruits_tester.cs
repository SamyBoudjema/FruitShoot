using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fruits_tester : MonoBehaviour {

	public GameObject[] fruits;
	// Use this for initialization
	void Start () 
	{
		
	}
	
	// Update is called once per frame
	void Update () 
	{
		
		fruits[0].transform.Rotate (Vector3.up, Time.deltaTime * 50f, Space.World);
		fruits[1].transform.Rotate (Vector3.up, -Time.deltaTime * 50f, Space.World);
		fruits[2].transform.Rotate (Vector3.up, Time.deltaTime * 50f, Space.World);
		fruits[3].transform.Rotate (Vector3.up, -Time.deltaTime * 50f, Space.World);
		fruits[4].transform.Rotate (Vector3.up, Time.deltaTime * 50f, Space.World);
		fruits[5].transform.Rotate (Vector3.up, -Time.deltaTime * 50f, Space.World);
		fruits[6].transform.Rotate (Vector3.up, Time.deltaTime * 50f, Space.World);
		fruits[7].transform.Rotate (Vector3.up, -Time.deltaTime * 50f, Space.World);
		fruits[8].transform.Rotate (Vector3.up, Time.deltaTime * 50f, Space.World);
		fruits[9].transform.Rotate (Vector3.up, -Time.deltaTime * 50f, Space.World);
	
	}
}

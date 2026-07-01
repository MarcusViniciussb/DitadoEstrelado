using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fruits_tester : MonoBehaviour {

	public GameObject[] fruits;

	void Start ()
	{

	}

	void Update ()
	{
		// Rotate(eixo, graus) substitui o obsoleto RotateAround(eixo, radianos)
		// Time.deltaTime * 60f = ~60 graus/s de velocidade de rotação
		fruits[0].transform.Rotate (Vector3.up,  Time.deltaTime * 60f);
		fruits[1].transform.Rotate (Vector3.up, -Time.deltaTime * 60f);
		fruits[2].transform.Rotate (Vector3.up,  Time.deltaTime * 60f);
		fruits[3].transform.Rotate (Vector3.up, -Time.deltaTime * 60f);
		fruits[4].transform.Rotate (Vector3.up,  Time.deltaTime * 60f);
		fruits[5].transform.Rotate (Vector3.up, -Time.deltaTime * 60f);
		fruits[6].transform.Rotate (Vector3.up,  Time.deltaTime * 60f);
		fruits[7].transform.Rotate (Vector3.up, -Time.deltaTime * 60f);
		fruits[8].transform.Rotate (Vector3.up,  Time.deltaTime * 60f);
		fruits[9].transform.Rotate (Vector3.up, -Time.deltaTime * 60f);

	}
}

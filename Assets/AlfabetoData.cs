using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NovoAlfabeto", menuName = "IA/Banco de LIBRAS")]
public class AlfabetoData : ScriptableObject
{
    [System.Serializable]
    public struct LetraPadrao
    {
        public string nome;
        public Vector3[] pontosNormalizados;
    }
    public List<LetraPadrao> letrasGravadas = new List<LetraPadrao>();
}
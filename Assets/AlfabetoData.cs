using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NovoAlfabeto", menuName = "IA/Banco de LIBRAS")]
public class AlfabetoData : ScriptableObject
{
    // ── Letras estáticas: uma "foto" dos 21 pontos ──────────────────────────
    [System.Serializable]
    public struct LetraPadrao
    {
        public string nome;
        public Vector3[] pontosNormalizados;
    }
    public List<LetraPadrao> letrasGravadas = new List<LetraPadrao>();

    // ── Letras dinâmicas (H, J, K, X, Z...): um "filme" do movimento ────────
    [System.Serializable]
    public struct QuadroDeMao
    {
        public Vector3[] pontos; // 21 pontos relativos ao pulso, num instante
    }

    [System.Serializable]
    public struct SinalDinamico
    {
        public string nome;
        public List<QuadroDeMao> quadros; // sequência de ~1,3s do movimento
    }
    public List<SinalDinamico> sinaisDinamicos = new List<SinalDinamico>();
}

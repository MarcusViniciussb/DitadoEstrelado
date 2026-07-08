using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

// ReproduzirAnimacao: toca um clipe de animação de um modelo em LOOP,
// sem precisar de Animator Controller nem de importação Legacy.
// Usa a Playables API do Unity e reinicia o clipe manualmente ao terminar.
//
// Uso: ReproduzirAnimacao.Tocar(objeto, "caminho/do/modelo/em/Resources");
public class ReproduzirAnimacao : MonoBehaviour
{
    private AnimationClip         clipe;
    private PlayableGraph         grafo;
    private AnimationClipPlayable playable;
    private bool                  pronto = false;

    // Retorna true se encontrou e tocou uma animação (false = modelo estático)
    public static bool Tocar(GameObject alvo, string caminhoResources)
    {
        // Os clipes moram DENTRO do arquivo FBX — LoadAll pega os sub-assets
        var clipes = Resources.LoadAll<AnimationClip>(caminhoResources);
        if (clipes == null || clipes.Length == 0) return false; // sem animação

        // Prefere um clipe chamado "Idle" (parado respirando); senão o primeiro
        AnimationClip escolhido = clipes[0];
        foreach (var c in clipes)
            if (c.name.ToLower().Contains("idle")) { escolhido = c; break; }

        var animator = alvo.GetComponentInChildren<Animator>();
        if (animator == null) animator = alvo.AddComponent<Animator>();

        var reprodutor = alvo.AddComponent<ReproduzirAnimacao>();
        reprodutor.Iniciar(animator, escolhido);
        return true;
    }

    void Iniciar(Animator animator, AnimationClip clipeEscolhido)
    {
        clipe    = clipeEscolhido;
        grafo    = PlayableGraph.Create("AnimacaoDoObjeto");
        playable = AnimationClipPlayable.Create(grafo, clipe);

        var saida = AnimationPlayableOutput.Create(grafo, "saida", animator);
        saida.SetSourcePlayable(playable);
        grafo.Play();
        pronto = true;
    }

    void Update()
    {
        // Loop manual: quando o clipe chega ao fim, volta ao começo
        if (pronto && playable.IsValid() && playable.GetTime() >= clipe.length)
            playable.SetTime(0);
    }

    void OnDestroy()
    {
        if (grafo.IsValid()) grafo.Destroy();
    }
}

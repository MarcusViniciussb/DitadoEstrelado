// Endireita (e recorta) o quadro da webcam antes de qualquer uso.
//
// No celular a camera entrega a imagem girada em relacao a tela. Em vez de
// girar a interface e depois corrigir cada coordenada, o quadro e endireitado
// aqui: o detector, o desenho da mao e a imagem exibida passam a trabalhar
// no mesmo espaco, sem conversoes extras espalhadas pelo codigo.
Shader "DitadoEstrelado/RotacaoCamera"
{
    Properties
    {
        _MainTex ("Quadro da camera", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            // x,y = cosseno e seno do giro
            float4 _Giro;
            // x,y = centro do recorte (uv);  z,w = nao usado
            float4 _Centro;
            // x,y = tamanho da area de saida em pixels da FONTE
            // z,w = tamanho da textura de origem em pixels
            float4 _Tamanhos;

            struct entrada  { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct saida    { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            saida vert (entrada v)
            {
                saida o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (saida i) : SV_Target
            {
                // Posicao do pixel de saida, em pixels da imagem de origem
                float2 p = (i.uv - 0.5) * _Tamanhos.xy;

                // Giro inverso: leva o pixel de saida ate a origem certa
                float2 girado = float2( p.x * _Giro.x + p.y * _Giro.y,
                                       -p.x * _Giro.y + p.y * _Giro.x);

                float2 uv = _Centro.xy + girado / _Tamanhos.zw;
                return tex2D(_MainTex, saturate(uv));
            }
            ENDCG
        }
    }

    Fallback Off
}

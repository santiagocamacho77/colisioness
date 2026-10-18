using UnityEngine;

/// <summary>Una etapa del fondo (atardecer, noche, espacio...). Se aplica por completo desde 'startHeight'.</summary>
[System.Serializable]
public class BackgroundStage
{
    public string name = "Etapa";
    [Tooltip("Altura (m) a la que la etapa queda aplicada por completo.")]
    public float startHeight;
    [Tooltip("Color del cielo en el borde superior de la pantalla.")]
    public Color topColor = new Color(0.1f, 0.1f, 0.3f, 1f);
    [Tooltip("Color del cielo en el borde inferior de la pantalla.")]
    public Color bottomColor = new Color(0.5f, 0.4f, 0.6f, 1f);
    [Range(0f, 1f)] public float stars;
    [Range(0f, 1f)] public float clouds;
    public Color cloudColor = Color.white;
    [Tooltip("Color e intensidad multiplicadora de la luz direccional.")]
    public Color lightColor = Color.white;
    [Range(0f, 2f)] public float lightIntensity = 1f;

    public BackgroundStage() { }

    public BackgroundStage(string name, float startHeight, Color top, Color bottom, float stars, float clouds,
                           Color cloudColor, Color lightColor, float lightIntensity)
    {
        this.name = name;
        this.startHeight = startHeight;
        topColor = top;
        bottomColor = bottom;
        this.stars = stars;
        this.clouds = clouds;
        this.cloudColor = cloudColor;
        this.lightColor = lightColor;
        this.lightIntensity = lightIntensity;
    }
}

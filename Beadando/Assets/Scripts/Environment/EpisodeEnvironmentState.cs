using UnityEngine;

public static class EpisodeEnvironmentState
{
    public static Vector3 PredatorSpawnLocalPosition { get; private set; }
    public static Vector3 PreySpawnLocalPosition { get; private set; }
    public static Vector3 WindAcceleration { get; private set; }
    public static float RainIntensity { get; private set; }
    public static bool HasPreparedEpisode { get; private set; }

    public static void PrepareEpisode(
        Vector3 predatorSpawnOrigin,
        float predatorSpawnRadius,
        Vector3 preySpawnOrigin,
        float preySpawnRadius,
        float minimumSpawnSeparation,
        float maxWindAcceleration,
        float maxRainIntensity)
    {
        PredatorSpawnLocalPosition = predatorSpawnOrigin;
        PreySpawnLocalPosition = preySpawnOrigin;

        float requiredSeparation = Mathf.Max(0f, minimumSpawnSeparation);

        for (int attempt = 0; attempt < 12; attempt++)
        {
            PredatorSpawnLocalPosition = predatorSpawnOrigin + GetRandomHorizontalOffset(predatorSpawnRadius);
            PreySpawnLocalPosition = preySpawnOrigin + GetRandomHorizontalOffset(preySpawnRadius);

            Vector3 horizontalOffset = PredatorSpawnLocalPosition - PreySpawnLocalPosition;
            horizontalOffset.y = 0f;

            if (horizontalOffset.sqrMagnitude >= requiredSeparation * requiredSeparation)
            {
                break;
            }
        }

        Vector3 windAcceleration = GetRandomHorizontalOffset(maxWindAcceleration);
        windAcceleration.y = 0f;
        WindAcceleration = windAcceleration;
        RainIntensity = Random.Range(0f, Mathf.Max(0f, maxRainIntensity));
        HasPreparedEpisode = true;
    }

    public static bool TryGetPreparedPreySpawn(out Vector3 spawnLocalPosition)
    {
        spawnLocalPosition = PreySpawnLocalPosition;
        return HasPreparedEpisode;
    }

    public static void ApplyWeatherVisuals(
        Color clearFogColor,
        Color stormFogColor,
        float clearFogDensity,
        float stormFogDensity,
        Color clearLightColor,
        Color stormLightColor,
        float clearLightIntensity,
        float stormLightIntensity)
    {
        RenderSettings.fogColor = Color.Lerp(clearFogColor, stormFogColor, RainIntensity);
        RenderSettings.fogDensity = Mathf.Lerp(clearFogDensity, stormFogDensity, RainIntensity);

        Light directionalLight = FindDirectionalLight();

        if (directionalLight == null)
        {
            return;
        }

        directionalLight.color = Color.Lerp(clearLightColor, stormLightColor, RainIntensity);
        directionalLight.intensity = Mathf.Lerp(clearLightIntensity, stormLightIntensity, RainIntensity);
    }

    private static Vector3 GetRandomHorizontalOffset(float radius)
    {
        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, radius);
        return new Vector3(offset.x, 0f, offset.y);
    }

    private static Light FindDirectionalLight()
    {
        Light[] sceneLights = Object.FindObjectsOfType<Light>();

        foreach (Light sceneLight in sceneLights)
        {
            if (sceneLight.type == LightType.Directional)
            {
                return sceneLight;
            }
        }

        return null;
    }
}

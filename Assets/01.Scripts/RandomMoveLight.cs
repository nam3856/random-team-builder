using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RandomMoveLight : MonoBehaviour
{
    [SerializeField] private Light2D[] movingLights;
    [SerializeField] private Transform[] teamTargets;

    private bool isFocusing;
    private int currentLightIndex;

    public int TeamTargetCount => teamTargets?.Length ?? 0;

    public void GoLight()
    {
        if (movingLights == null || movingLights.Length == 0)
            return;

        StartCoroutine(RandomMoveLights());
    }

    private IEnumerator RandomMoveLights()
    {
        isFocusing = true;
        yield return new WaitForSeconds(0.5f);
        isFocusing = false;

        while (!isFocusing)
        {
            foreach (Light2D light in movingLights)
            {
                if (light == null)
                    continue;

                Vector3 randomPosition = GetRandomSafeViewportPosition(light.transform.position.z);

                light.transform.DOMove(randomPosition, 1.5f).SetEase(Ease.InOutExpo);
                light.GetComponent<NativeLightPulse>()?.Play();
            }

            yield return new WaitForSeconds(1.5f);
        }
    }

    public void FocusLightOnTeam(int teamIndex)
    {
        if (movingLights == null || movingLights.Length == 0 ||
            teamTargets == null || teamIndex < 0 || teamIndex >= teamTargets.Length ||
            teamTargets[teamIndex] == null)
        {
            Debug.LogWarning($"팀 {teamIndex + 1}의 라이트 타깃을 찾을 수 없습니다.");
            return;
        }

        isFocusing = true;

        Light2D light = movingLights[currentLightIndex];
        currentLightIndex = (currentLightIndex + 1) % movingLights.Length;
        if (light == null)
            return;

        Vector3 targetPosition = teamTargets[teamIndex].position + new Vector3(0f, 1f, -3f);
        light.transform.DOMove(targetPosition, 0.8f).SetEase(Ease.OutBack);
    }

    private static Vector3 GetRandomSafeViewportPosition(float targetZ)
    {
        Camera camera = Camera.main;
        if (camera == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return new Vector3(
                Random.Range(-8f, 7f),
                Random.Range(-4.5f, 4.5f),
                targetZ);
        }

        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
            safeArea = new Rect(0f, 0f, Screen.width, Screen.height);

        const float margin = 0.08f;
        float xMin = Mathf.Lerp(safeArea.xMin / Screen.width, safeArea.xMax / Screen.width, margin);
        float xMax = Mathf.Lerp(safeArea.xMin / Screen.width, safeArea.xMax / Screen.width, 1f - margin);
        float yMin = Mathf.Lerp(safeArea.yMin / Screen.height, safeArea.yMax / Screen.height, margin);
        float yMax = Mathf.Lerp(safeArea.yMin / Screen.height, safeArea.yMax / Screen.height, 1f - margin);
        float distance = Mathf.Abs(targetZ - camera.transform.position.z);
        Vector3 minimum = camera.ViewportToWorldPoint(new Vector3(xMin, yMin, distance));
        Vector3 maximum = camera.ViewportToWorldPoint(new Vector3(xMax, yMax, distance));

        return new Vector3(
            Random.Range(minimum.x, maximum.x),
            Random.Range(minimum.y, maximum.y),
            targetZ);
    }
}

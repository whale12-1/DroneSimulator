using UnityEngine;

[System.Serializable]
public class PIDController
{
    public float pFactor; // K_p
    public float iFactor; // K_i
    public float dFactor; // K_d

    private float integral;
    private float lastError;

    public PIDController(float p, float i, float d)
    {
        pFactor = p;
        iFactor = i;
        dFactor = d;
    }

    public float Update(float error, float deltaTime)
    {
        if (deltaTime <= 0f) return 0f;

        // 1. ѕропорциональна€ составл€юща€: P = K_p * error
        float pVal = error * pFactor;

        // 2. »нтегральна€ составл€юща€: I = K_i * sum(error * dt)
        integral += error * deltaTime;

        // Anti-windup (ограничение интеграла, чтобы дрон не "залипал" при накоплении большой ошибки)
        integral = Mathf.Clamp(integral, -100f, 100f);

        float iVal = integral * iFactor;

        // 3. ƒифференциальна€ составл€юща€: D = K_d * (error - lastError) / dt
        float errorDerivative = (error - lastError) / deltaTime;
        lastError = error; // сохран€ем текущую ошибку дл€ следующего кадра

        float dVal = errorDerivative * dFactor;

        // »тоговый выходной сигнал регул€тора
        return pVal + iVal + dVal;
    }

    public void Reset()
    {
        integral = 0f;
        lastError = 0f;
    }
}
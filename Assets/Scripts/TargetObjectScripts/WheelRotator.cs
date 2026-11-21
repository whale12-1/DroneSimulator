using UnityEngine;

public class WheelRotator : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private Transform[] wheels;
    [SerializeField] private float wheelRadius = 0.5f;
    [SerializeField] private Vector3 rotationAxis = Vector3.right;

    private Vector3 lastPosition;
    private Transform[] wheelPivots; // Здесь будут храниться наши новые центры вращения

    private void Start()
    {
        lastPosition = transform.position;
        wheelPivots = new Transform[wheels.Length];

        // АВТОМАТИЧЕСКАЯ ПОЧИНКА ПИВОТОВ
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] == null) continue;

            Renderer rend = wheels[i].GetComponentInChildren<Renderer>();
            if (rend == null) continue;

            // 1. Создаем пустышку для правильного вращения
            GameObject pivotObj = new GameObject($"Pivot_{wheels[i].name}");

            // 2. Ставим ее ровно в визуальный центр колеса
            pivotObj.transform.position = rend.bounds.center;

            // 3. Делаем ее родителем для колеса (сохраняя его мировую позицию)
            pivotObj.transform.SetParent(wheels[i].parent, true);
            wheels[i].SetParent(pivotObj.transform, true);

            wheelPivots[i] = pivotObj.transform;
        }
    }

    private void Update()
    {
        float currentSpeed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
        lastPosition = transform.position;

        float rotationAmount = (currentSpeed / wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;

        for (int i = 0; i < wheelPivots.Length; i++)
        {
            if (wheelPivots[i] != null)
            {
                // Теперь мы вращаем пустышку, которая стоит ровно в центре!
                wheelPivots[i].Rotate(rotationAxis, rotationAmount, Space.Self);
            }
        }
    }
}
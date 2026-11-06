using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Singleton de recursos. Uso: ResourceManager.Instance.CrearOro(posicion)
/// </summary>
public class ResourceManager : MonoBehaviour
{
    public TextMeshProUGUI oroTextDisplay;
    public TextMeshProUGUI workerTextDisplayCount;
    public TextMeshProUGUI workerTextCostCreation;
    public TextMeshProUGUI bonesTextDisplayCount;

    public Transform workersHolder;
    public Transform goldHolder;

    public static Action<int> GoldHasChange;
    public static Action<int> WorkersHasChange;
    public static Action<float> WorkerSpeedHasChange;

    public static ResourceManager Instance { get; private set; }

    public GameObject goldPrefab;
    public Worker workerPrefab;

    [Header("Oro por pilar")]
    [Tooltip("Cantidad mínima de oro que consigue un worker al terminar un pilar.")]
    public int oroMinimo = 1;
    [Tooltip("Cantidad máxima de oro que consigue un worker al terminar un pilar.")]
    public int oroMaximo = 3;

    public int workerSpeed = 3;

    [Header("Pila de oro")]
    [Tooltip("Lugar donde los workers juntan el oro.")]
    public Vector3 puntoDeEntrega = Vector3.zero;
    [Tooltip("Máximo de objetos de oro en la pila. Por encima de esto el oro sólo suma al número (para no laguear).")]
    public int maxObjetosOro = 1000;

    [Header("Regeneración de pilares")]
    [Tooltip("Hasta esta distancia al centro, los pilares tardan 'segundosRegeneracionCerca' en volver.")]
    [FormerlySerializedAs("distanciaLejana")]
    public float distanciaCerca = 15f;
    [Tooltip("Segundos que tarda en volver un pilar que está dentro de 'distanciaCerca'.")]
    [FormerlySerializedAs("segundosRegeneracionCentro")]
    public float segundosRegeneracionCerca = 60f;
    [Tooltip("Distancia a partir de la cual los pilares vuelven en el tiempo mínimo. Entre 'distanciaCerca' y esta, el tiempo va bajando de a poco.")]
    public float distanciaMaxima = 60f;
    [Tooltip("Segundos que tarda en volver un pilar a 'distanciaMaxima' o más (lo más rápido posible).")]
    [FormerlySerializedAs("segundosRegeneracionLejos")]
    public float segundosRegeneracionMinimo = 10f;
    [Tooltip("Variación al azar del tiempo (0.25 = ±25%), para que no vuelvan todos juntos.")]
    [Range(0f, 1f)] public float variacionRegeneracion = 0.25f;
    [Tooltip("Segundos que tarda en subir desde abajo hasta estar funcional.")]
    public float segundosCrecimiento = 5f;
    [Tooltip("Material del pilar mientras crece (para distinguirlo).")]
    public Material materialPilarCreciendo;

    public int CostoDeTrabajador = 5;
    public int BonesCounter = 0;
    [Tooltip("Segundos sin huesos después de encontrar uno (cuenta tiempo de juego: no corre mientras se elige el perk).")]
    public float cooldownHuesos = 30f;
    /// <summary>Oro total entregado en la pila.</summary>
    public int Oro { get; private set; }
    private int workersCount = 1;
    // Objetos de oro que están en la pila, en el orden en que llegaron (los últimos quedan arriba)
    private readonly List<GameObject> oroEnPila = new List<GameObject>();
    /// <summary>Se dispara cada vez que cambia el oro (para la UI, etc.).</summary>
    public event Action<int> OroCambiado;

    /// <summary>Se dispara una vez por segundo (cualquier script se puede suscribir).</summary>
    public event Action Segundo;

    // Pilares desactivados esperando volver, con los segundos que les faltan
    private class PilarEnEspera { public Pilar pilar; public int segundos; }
    private readonly List<PilarEnEspera> pilaresEnEspera = new List<PilarEnEspera>();

    // Pilares que están subiendo (se animan acá, no en el pilar)
    private class PilarCreciendo { public Pilar pilar; public float progreso; }
    private readonly List<PilarCreciendo> pilaresCreciendo = new List<PilarCreciendo>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Hay más de un ResourceManager en la escena; se destruye el duplicado.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    private void OnEnable()
    {
        Pilar.HuesoEncontrado += OnBoneFound;
    }
    private void OnDisable()
    {
        Pilar.HuesoEncontrado -= OnBoneFound;

    }

    private void OnBoneFound(Pilar pilar)
    {
        BonesCounter++;
        bonesTextDisplayCount.SetText(BonesCounter.ToString());
    }

    private void Start()
    {
        UpdateCost();
        InvokeRepeating(nameof(TickSegundo), 1f, 1f);
    }

    // ---------- Tick de 1 segundo ----------

    private void TickSegundo()
    {
        ActualizarPilaresEnEspera();
        Segundo?.Invoke();
    }

    /// <summary>Lo llama el pilar cuando terminó de hundirse: se desactiva y espera para volver.</summary>
    public void RegistrarPilarConsumido(Pilar pilar)
    {
        pilar.gameObject.SetActive(false);
        pilaresEnEspera.Add(new PilarEnEspera { pilar = pilar, segundos = SegundosRegeneracion(pilar) });
    }

    /// <summary>Distancia en XZ desde una posición al punto de entrega (el centro).</summary>
    public float DistanciaAlCentro(Vector3 posicion)
    {
        Vector3 d = posicion - puntoDeEntrega;
        d.y = 0f;
        return d.magnitude;
    }

    /// <summary>Segundos que tarda en volver un pilar a esa distancia, sin la variación al azar.</summary>
    public float SegundosRegeneracionBase(float distancia)
    {
        // Dentro de 'cerca': tiempo fijo. Después baja progresivamente hasta el mínimo en 'distanciaMaxima'.
        if (distancia <= distanciaCerca) return segundosRegeneracionCerca;
        float rango = distanciaMaxima - distanciaCerca;
        float t = rango > 0f ? Mathf.Clamp01((distancia - distanciaCerca) / rango) : 1f;
        return Mathf.Lerp(segundosRegeneracionCerca, segundosRegeneracionMinimo, t);
    }

    // Más lejos del centro = vuelve más rápido
    private int SegundosRegeneracion(Pilar pilar)
    {
        float segundos = SegundosRegeneracionBase(DistanciaAlCentro(pilar.transform.position));
        segundos *= 1f + UnityEngine.Random.Range(-variacionRegeneracion, variacionRegeneracion);
        return Mathf.Max(1, Mathf.RoundToInt(segundos));
    }

    private void ActualizarPilaresEnEspera()
    {
        for (int i = pilaresEnEspera.Count - 1; i >= 0; i--)
        {
            PilarEnEspera e = pilaresEnEspera[i];
            if (e.pilar == null) { pilaresEnEspera.RemoveAt(i); continue; } // fue destruido (ej. al regenerar el mapa)

            e.segundos--;
            if (e.segundos > 0) continue;

            // Si hay un worker parado ahí, esperar al próximo segundo
            if (e.pilar.LugarOcupado()) continue;

            e.pilar.EmpezarCrecimiento(materialPilarCreciendo);
            pilaresCreciendo.Add(new PilarCreciendo { pilar = e.pilar, progreso = 0f });
            pilaresEnEspera.RemoveAt(i);
        }
    }

    void Update()
    {
        if (pilaresCreciendo.Count == 0) return;

        float paso = Time.deltaTime / Mathf.Max(0.01f, segundosCrecimiento);
        for (int i = pilaresCreciendo.Count - 1; i >= 0; i--)
        {
            PilarCreciendo c = pilaresCreciendo[i];
            if (c.pilar == null) { pilaresCreciendo.RemoveAt(i); continue; }

            c.progreso += paso;
            if (c.progreso >= 1f)
            {
                c.pilar.TerminarCrecimiento();
                pilaresCreciendo.RemoveAt(i);
            }
            else
            {
                c.pilar.AvanzarCrecimiento(c.progreso);
            }
        }
    }

    /// <summary>Cuántos oros da un pilar terminado (al azar entre oroMinimo y oroMaximo, ambos incluidos).</summary>
    public int OroPorPilar()
    {
        return UnityEngine.Random.Range(oroMinimo, Mathf.Max(oroMinimo, oroMaximo) + 1);
    }

    /// <summary>Instancia un objeto de oro en la posición dada.</summary>
    public GameObject CrearOro(Vector3 posicion)
    {
        if (goldPrefab == null)
        {
            Debug.LogError("ResourceManager: falta asignar goldPrefab.", this);
            return null;
        }
        return Instantiate(goldPrefab, posicion, Quaternion.identity, goldHolder);
    }

    /// <summary>Lo llama el worker cuando un oro cae en la pila.</summary>
    public void OroEntregado(GameObject oro)
    {
        if (oro != null)
        {
            // Si la pila ya está llena, el oro sólo cuenta como número
            if (CantidadObjetosEnPila() < maxObjetosOro) oroEnPila.Add(oro);
            else Destroy(oro);
        }
        Oro++;
        OroCambiado?.Invoke(Oro);
        UpdateGold();
    }
    /// <summary>
    /// Multiplica el oro. Crea objetos de oro nuevos sobre la pila sólo hasta llegar a maxObjetosOro;
    /// el resto sólo suma al número.
    /// </summary>
    public void MutiplyGold(int val)
    {
        if (val <= 1)
        {
            Debug.LogWarning($"MutiplyGold: el multiplicador es {val}, tiene que ser 2 o más (revisá el 'value' del perk en el Inspector).", this);
            return;
        }

        int extra = Oro * (val - 1);
        Oro += extra;

        int objetosACrear = Mathf.Clamp(maxObjetosOro - CantidadObjetosEnPila(), 0, extra);
        float alturaBase = AlturaArribaDeLaPila();

        for (int i = 0; i < objetosACrear; i++)
        {
            // Caen desde arriba de la pila, escalonados para que no aparezcan uno dentro del otro
            Vector2 r = UnityEngine.Random.insideUnitCircle * 0.8f;
            Vector3 pos = puntoDeEntrega + new Vector3(r.x, 0f, r.y);
            pos.y = alturaBase + 1f + i * 0.35f;

            GameObject oro = CrearOro(pos);
            if (oro == null) break;
            oroEnPila.Add(oro);
        }

        OroCambiado?.Invoke(Oro);
        UpdateGold();
    }

    // Objetos de oro que siguen existiendo en la pila (limpia los que se destruyeron por otro lado)
    private int CantidadObjetosEnPila()
    {
        oroEnPila.RemoveAll(o => o == null);
        return oroEnPila.Count;
    }

    // Altura de lo más alto que hay en el punto de entrega (piso o pila de oro)
    private float AlturaArribaDeLaPila()
    {
        Vector3 desde = puntoDeEntrega + Vector3.up * 50f;
        if (Physics.Raycast(desde, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return puntoDeEntrega.y;
    }
    public void AddWorkerMinGold()
    {
        oroMinimo += 1;
        if(oroMinimo > oroMaximo)
            oroMaximo = oroMinimo;
    }
    public void AddWorkerMaxGold()
    {
               oroMaximo += 2;
    }
    public void AddWokerSpeed()
    {
        workerSpeed += 1;
        WorkerSpeedHasChange?.Invoke(workerSpeed);
    }
    private void UpdateGold()
        {
        oroTextDisplay.SetText(Oro.ToString());
        GoldHasChange?.Invoke(Oro);
    }
    private void UpdateCost()
    {
        workerTextCostCreation.SetText("Contratar trabajador por " + CostoDeTrabajador + " de oro");
    }
    /// <summary>
    /// Gasta oro: resta la cantidad y, si quedan más objetos en la pila que oro, borra los que sobran
    /// (empezando por los últimos que llegaron, que son los de arriba). Así la pila nunca muestra
    /// más oro del que tenés, y por debajo de maxObjetosOro cada objeto vuelve a ser 1 de oro.
    /// Devuelve false si no alcanza el oro (y no gasta nada).
    /// </summary>
    public bool SustractGold(int num)
    {
        if (num <= 0) return true;
        if (num > Oro) return false;

        Oro -= num;

        int sobran = CantidadObjetosEnPila() - Oro; // limpia nulos una sola vez
        for (int i = oroEnPila.Count - 1; i >= 0 && sobran > 0; i--, sobran--)
        {
            Destroy(oroEnPila[i]);
            oroEnPila.RemoveAt(i);
        }

        OroCambiado?.Invoke(Oro);
        UpdateGold();
        return true;
    }
    public void SpownWorker()
    {
        if (workerPrefab == null)
        {
            Debug.LogError("ResourceManager: falta asignar workerPrefab.", this);
            return;
        }

        if (CostoDeTrabajador <= Oro)
        { 
            Instantiate(workerPrefab, new Vector3(0, 0, 0), Quaternion.identity, workersHolder);
            workersCount++;
            workerTextDisplayCount.SetText(workersCount.ToString());
            SustractGold(CostoDeTrabajador);
             CostoDeTrabajador = Mathf.RoundToInt(CostoDeTrabajador * 1.5f)+2;
            UpdateCost();
            UpdateGold();
            WorkersHasChange?.Invoke(workersCount);
        }
    }
}

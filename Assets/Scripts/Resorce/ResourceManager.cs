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
    public TextMeshProUGUI toxicWhaterDisplayCount;
    public TextMeshProUGUI costoDeLimpiarRoxinasDisplay;

    public Transform workersHolder;
    public Transform goldHolder;

    public static Action<int> GoldHasChange;
    public static Action<int> WorkersHasChange;
    public static Action<float> WorkerSpeedHasChange;
    public static Action GoldHasCleaned;
    public static Action<int> PlayerHasNoGold;
    public static Action<Material> WhaterHasChange;
    public static Action WorkerIsDead;

    public Material whaterIn0;
    public Material whaterIn20;
    public Material whaterIn40;
    public Material whaterIn60;
    public Material whaterIn80;
    public Material whaterIn100;
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
    [Tooltip("Segundos que tarda un worker en lavar el oro en el agua (similar a picar una piedra).")]
    public float segundosLavadoOro = 3f;
    [Tooltip("Material del oro cuando el worker lo carga (sucio, antes de lavarlo).")]
    public Material materialOroSucio;
    [Tooltip("Material del oro ya lavado. Si queda vacío se usa el material del goldPrefab.")]
    public Material materialOroLimpio;
    [Tooltip("Máximo de objetos de oro en la pila. Por encima de esto el oro sólo suma al número (para no laguear).")]
    public int maxObjetosOro = 1000;

    [Header("Multiplicar oro (perk)")]
    [Tooltip("Tiempo máximo que tarda en crear todo el oro del perk de multiplicar.")]
    public float duracionMaxMultiplicar = 2f;
    [Tooltip("Pausa entre cada oro cuando es poco oro (con mucho oro va más rápido para no pasarse del tiempo máximo).")]
    public float intervaloMaxMultiplicar = 0.08f;
    [Tooltip("Separación mínima entre sonidos de oro mientras se multiplica.")]
    public float intervaloMinSonidoOro = 0.05f;

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
    /// <summary>Oro total entregado en la pila.</summary>
    public int Oro;
    private int workersCount = 1;
    public float toxicWaterCount = 0;

    /// <summary>
    /// % de chance de que muera un worker al lavar: toxinas / 20, truncado (sin redondear).
    /// 19 → 0%, 25 → 1%, 39 → 1%, 40 → 2%...
    /// </summary>
    public int ChanceMuerteTrabajador => Mathf.FloorToInt(toxicWaterCount / 20f);
    public float workerToxicAdd = 1f;

    public int costoDeLimpiarRoxinas = 10;
    // Objetos de oro que están en la pila, en el orden en que llegaron (los últimos quedan arriba)
    private readonly List<GameObject> oroEnPila = new List<GameObject>();
    /// <summary>Se dispara cada vez que cambia el oro (para la UI, etc.).</summary>
    public event Action<int> OroCambiado;

    /// <summary>Se dispara una vez por segundo (cualquier script se puede suscribir).</summary>
    public event Action Segundo;

    public int chanceToDobleGold = 0;
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
        GoldHasCleaned += OnGoldCleanded;
        WorkerIsDead += OnWokerDied;

    }
    private void OnDisable()
    {
        Pilar.HuesoEncontrado -= OnBoneFound;
        GoldHasCleaned -= OnGoldCleanded;
        WorkerIsDead -= OnWokerDied;

    }

    private void OnWokerDied()
    {
        workersCount--;
        workerTextDisplayCount.SetText(workersCount.ToString());
        WorkersHasChange?.Invoke(workersCount);
    }

    private void OnGoldCleanded()
    {
        toxicWaterCount = Mathf.Min(toxicWaterCount + workerToxicAdd, 100f);
        toxicWhaterDisplayCount.SetText(toxicWaterCount.ToString("F2") + "%");
        ActualizarMaterialAgua();
    }

    // ---------- Material del agua según la contaminación ----------

    private Material materialAguaActual;

    /// <summary>Material que tiene que tener el agua ahora (lo usa cada Whater al aparecer).</summary>
    public Material MaterialAguaActual => materialAguaActual != null ? materialAguaActual : MaterialAguaPara(toxicWaterCount);

    // 0-19 → whaterIn0, 20-39 → whaterIn20, 40-59 → whaterIn40, ... 100 → whaterIn100.
    // Si el material de ese nivel no está asignado, usa el del nivel anterior.
    private Material MaterialAguaPara(float toxinas)
    {
        Material[] niveles = { whaterIn0, whaterIn20, whaterIn40, whaterIn60, whaterIn80, whaterIn100 };
        int nivel = Mathf.Clamp(Mathf.FloorToInt(toxinas / 20f), 0, niveles.Length - 1);
        for (int i = nivel; i >= 0; i--)
            if (niveles[i] != null) return niveles[i];
        return null;
    }

    // Avisa a toda el agua sólo cuando cambia de nivel (no en cada lavado)
    private void ActualizarMaterialAgua()
    {
        Material m = MaterialAguaPara(toxicWaterCount);
        if (m == null || m == materialAguaActual) return;
        materialAguaActual = m;
        WhaterHasChange?.Invoke(m);
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

    /// <summary>Material del oro limpio (el asignado, o el del goldPrefab si no hay).</summary>
    public Material MaterialOroLimpio
    {
        get
        {
            if (materialOroLimpio != null) return materialOroLimpio;
            var r = goldPrefab != null ? goldPrefab.GetComponentInChildren<Renderer>() : null;
            return r != null ? r.sharedMaterial : null;
        }
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
    public void OroEntregado(GameObject oro, bool conSonido = true)
    {
        if (oro != null)
        {
            // Si la pila ya está llena, el oro sólo cuenta como número
            if (CantidadObjetosEnPila() < maxObjetosOro) oroEnPila.Add(oro);
            else Destroy(oro);
        }
        Oro++;
        OroCambiado?.Invoke(Oro);
        if (conSonido && AudioMaster.Instance != null)
            AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.goldGain, puntoDeEntrega);
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
        if (extra <= 0) return;
        StartCoroutine(MultiplicarOroGradual(extra));
    }

    // Va creando el oro de a poco (y subiendo el número) para que sea un momento de "lluvia de oro".
    // Nunca tarda más de 'duracionMaxMultiplicar': con mucho oro crea varios por frame.
    private System.Collections.IEnumerator MultiplicarOroGradual(int extra)
    {
        int objetos = Mathf.Clamp(maxObjetosOro - oroEnPila.Count, 0, extra);
        int pasos = Mathf.Max(1, objetos);
        float intervalo = Mathf.Min(intervaloMaxMultiplicar, duracionMaxMultiplicar / pasos);

        int agregado = 0;
        int i = 0;
        float t = 0f;
        float ultimoSonido = -999f;
        float alturaBase = AlturaArribaDeLaPila();

        while (true)
        {
            // Cuántos pasos tendrían que haberse hecho a esta altura del tiempo
            int hasta = Mathf.Min(pasos, Mathf.FloorToInt(t / intervalo) + 1);
            int antes = i;
            for (; i < hasta; i++)
            {
                if (i < objetos && oroEnPila.Count < maxObjetosOro)
                {
                    if (i % 20 == 0) alturaBase = AlturaArribaDeLaPila();
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 0.8f;
                    Vector3 pos = puntoDeEntrega + new Vector3(r.x, alturaBase + 2f, r.y);
                    GameObject oro = CrearOro(pos);
                    if (oro != null) oroEnPila.Add(oro);
                }

                // El número sube junto con los objetos (repartido, por si hay más oro que objetos)
                int deberia = Mathf.RoundToInt(extra * (i + 1f) / pasos);
                Oro += deberia - agregado;
                agregado = deberia;
            }

            if (i > antes) // sólo si en este frame apareció oro
            {
                OroCambiado?.Invoke(Oro);
                UpdateGold();

                // Sonido con separación mínima (si no, cientos a la vez son ruido)
                if (Time.unscaledTime - ultimoSonido >= intervaloMinSonidoOro && AudioMaster.Instance != null)
                {
                    ultimoSonido = Time.unscaledTime;
                    AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.goldGain, puntoDeEntrega);
                }
            }

            if (i >= pasos) break;
            yield return null;
            t += Time.unscaledDeltaTime; // tiempo real: funciona aunque el juego esté pausado
        }
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
    public void AddWorkerMinGold(int min)
    {
        oroMinimo += min;
        oroMaximo += min;
    }
    public void AddWorkerMaxGold(int max)
    {
               oroMaximo += max;
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
        AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.goldSpend, puntoDeEntrega);
        return true;
    }
    public void ReduceToxicidadPerLavado()
    {
        workerToxicAdd = Mathf.Max(0f, workerToxicAdd - 0.1f);
    }
    public void ReduceToxicidad()
            {
        toxicWaterCount = Mathf.Max(0f, toxicWaterCount - 20f);
        toxicWhaterDisplayCount.SetText(toxicWaterCount.ToString("F2") + "%");
        ActualizarMaterialAgua();
        costoDeLimpiarRoxinas = Mathf.RoundToInt(costoDeLimpiarRoxinas * 1.3f) + 5;

        costoDeLimpiarRoxinasDisplay.SetText("Reducir 20% la toxicidad del agua por "+ costoDeLimpiarRoxinas + " oro");
    }

    public void ReduceToxixidadButton()
    {
               if (costoDeLimpiarRoxinas <= Oro)
        {
            SustractGold(costoDeLimpiarRoxinas);
            ReduceToxicidad();
        }
        else
        {
            PlayerHasNoGold?.Invoke(costoDeLimpiarRoxinas);
        }
    }

    public void SpownTrabajador()
    {
        Instantiate(workerPrefab, new Vector3(0, 0, 0), Quaternion.identity, workersHolder);
        workersCount++;
        workerTextDisplayCount.SetText(workersCount.ToString());
        CostoDeTrabajador = Mathf.RoundToInt(CostoDeTrabajador * 1.5f) + 2;
        UpdateCost();
        UpdateGold();
        WorkersHasChange?.Invoke(workersCount);
        AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.workerPop, puntoDeEntrega);
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
            SustractGold(CostoDeTrabajador);
            SpownTrabajador();
        }
        else
        {
            PlayerHasNoGold?.Invoke(CostoDeTrabajador);
        }
    }

    public void ChanceDobleOroAdd(int value)
    {
        chanceToDobleGold += value;
    }
}

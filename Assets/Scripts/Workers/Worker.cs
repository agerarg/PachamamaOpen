using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Worker : MonoBehaviour
{
    private enum Estado { Buscando, Yendo, Picando, Descansando, YendoAlAgua, Lavando, LlevandoOro, TirandoOro, Muerto }

    [Tooltip("Distancia máxima (en XZ) para buscar el punto de NavMesh más cercano al pilar.")]
    [SerializeField] private float sampleRadius = 5f;

    [Header("Picar piedra")]
    [Tooltip("Distancia (en XZ) desde el borde del pilar a la que puede picar.")]
    [SerializeField] private float rangoPicado = 0.8f;
    [Tooltip("Piedra que saca por golpe.")]
    [SerializeField] private float danioPorGolpe = 1f;
    [Tooltip("Segundos entre golpes.")]
    [SerializeField] private float intervaloGolpe = 1f;
    [Tooltip("Si se queda trabado este tiempo sin llegar, prueba con otro pilar.")]
    [SerializeField] private float tiempoMaximoTrabado = 2f;
    [Tooltip("Si en 'tiempoMaximoTrabado' segundos se movió menos que esto, se considera trabado.")]
    [SerializeField] private float distanciaMinimaProgreso = 0.5f;
    [Tooltip("Cuántos de los pilares más cercanos revisa por frame buscando uno con camino.")]
    [SerializeField] private int candidatosPorBusqueda = 5;
    [Tooltip("Segundos que ignora un pilar al que no hay camino (después lo vuelve a probar, por si se abrió paso).")]
    [SerializeField] private float segundosOlvidarInalcanzable = 5f;
    [Tooltip("Segundos que descansa después de terminar de recolectar un pilar.")]
    [SerializeField] private float cooldownAlTerminar = 1f;

    [Header("Oro")]
    [Tooltip("Dónde lleva el oro el worker, relativo a su posición (encima de la cabeza).")]
    [SerializeField] private Vector3 puntoDeCarga = new Vector3(0f, 1.3f, 0f);
    [Tooltip("Separación vertical entre los oros apilados que lleva.")]
    [SerializeField] private float separacionCarga = 0.32f;
    [Tooltip("Distancia (en XZ) al punto de entrega a la que empieza a tirar el oro.")]
    [SerializeField] private float distanciaEntrega = 2f;
    [Tooltip("Radio alrededor del punto de entrega donde caen los oros.")]
    [SerializeField] private float radioPila = 0.5f;
    [Tooltip("Altura extra de la curva al tirar el oro.")]
    [SerializeField] private float alturaTiro = 1.5f;
    [Tooltip("Segundos que tarda cada oro en llegar a la pila.")]
    [SerializeField] private float duracionTiro = 0.5f;
    [Tooltip("Segundos entre cada oro que tira.")]
    [SerializeField] private float intervaloTiro = 0.2f;
    [Tooltip("Tiempo máximo para tirar toda la carga: si lleva mucho oro, tira más seguido para no pasarse de este tiempo (la fuerza del tiro no cambia).")]
    [SerializeField] private float tiempoMaximoDescarga = 1.5f;

    [Header("Muerte")]
    [Tooltip("Segundos que queda quieto antes de desaparecer (para la animación de muerte más adelante).")]
    [SerializeField] private float segundosMuerte = 2f;

    [Header("Debug")]
    [SerializeField] private Estado estado = Estado.Buscando;
    [SerializeField] private Pilar objetivo;

    private NavMeshAgent agent;
    private float timerGolpe;
    private bool golpeSonado; // si ya sonó el golpe que está en curso
    // Detección de trabado: cada 'tiempoMaximoTrabado' se compara la posición con la anterior
    private Vector3 posVentanaTrabado;
    private float timerVentanaTrabado;
    private float timerCooldown;
    private bool sinPilares;
    // Pilares sin camino, con el momento hasta el que se ignoran
    private readonly Dictionary<Pilar, float> inalcanzables = new Dictionary<Pilar, float>();
    private readonly List<Pilar> candidatos = new List<Pilar>();
    private readonly List<Pilar> paraOlvidar = new List<Pilar>();
    private NavMeshPath camino;

    // Lavado del oro
    private Whater aguaObjetivo;
    private float timerLavado;
    private readonly HashSet<Whater> aguasInalcanzables = new HashSet<Whater>();
    // Pilares que este worker picó y todavía se están hundiendo (el worker los anima, no ellos)
    private readonly List<Pilar> hundiendo = new List<Pilar>();

    // Oro que lleva encima y oro que está volando hacia la pila (el worker los anima, no ellos)
    private class OroEnVuelo { public GameObject obj; public Vector3 desde, hasta; public float t; }
    private readonly List<GameObject> oroCargado = new List<GameObject>();
    private readonly List<OroEnVuelo> oroEnVuelo = new List<OroEnVuelo>();
    private float timerTiro;
    private float intervaloActual;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        camino = new NavMeshPath();

        agent.speed = ResourceManager.Instance != null ? ResourceManager.Instance.workerSpeed : 3f;
    }

    private static bool saliendo;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() { saliendo = false; Application.quitting += () => saliendo = true; }

    void OnEnable()  { 
        Pilar.PilarConsumido += AlConsumirsePilar;
        ResourceManager.WorkerSpeedHasChange += OnWorkerSpeedHasChange;


    }
    void OnDisable()
    {
        ResourceManager.WorkerSpeedHasChange -= OnWorkerSpeedHasChange;

        Pilar.PilarConsumido -= AlConsumirsePilar;
        // Si el worker desaparece en pleno juego, dejar los pilares que estaba hundiendo en su altura final
        // (no al cerrar el juego o descargar la escena, porque ahí se destruye todo igual)
        if (!saliendo && gameObject.scene.isLoaded)
        {
            for (int i = hundiendo.Count - 1; i >= 0; i--)
                if (hundiendo[i] != null) hundiendo[i].TerminarHundimiento();
            // Los oros en el aire llegan directo a la pila
            for (int i = oroEnVuelo.Count - 1; i >= 0; i--)
                if (oroEnVuelo[i].obj != null) Aterrizar(oroEnVuelo[i]);
        }
        hundiendo.Clear();
        oroEnVuelo.Clear();
    }

    private void OnWorkerSpeedHasChange(float obj)
    {
        agent.speed = ResourceManager.Instance != null ? ResourceManager.Instance.workerSpeed : 3f;
    }

    void Update()
    {
        // Quietos hasta que termine el fade de inicio
        if (!GameStart.Iniciado) return;

        switch (estado)
        {
            case Estado.Buscando:         Buscar();          break;
            case Estado.Yendo:            Ir();              break;
            case Estado.Picando:          Picar();           break;
            case Estado.Descansando:      Descansar();       break;
            case Estado.YendoAlAgua:      IrAlAgua();        break;
            case Estado.Lavando:          Lavar();           break;
            case Estado.LlevandoOro:      LlevarOro();       break;
            case Estado.TirandoOro:       TirarOro();        break;
            case Estado.Muerto:                              break; // quieto hasta desaparecer
        }

        AnimarHundimientos();
        AnimarOroEnVuelo();


      

    }

    // ---------- Picar piedra ----------

    private void Buscar()
    {
        // Si quedó fuera del NavMesh (ej. apareció en un lugar sin piso navegable), reubicarlo
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                agent.Warp(hit.position);
            return;
        }

        OlvidarInalcanzablesViejos();

        // Candidatos ordenados por distancia en línea recta (barato)
        Vector3 pos = transform.position;
        candidatos.Clear();
        foreach (Pilar p in Pilar.Todos)
            if (p != null && !p.Consumido && !EsInalcanzable(p)) candidatos.Add(p);

        sinPilares = candidatos.Count == 0;
        if (sinPilares)
        {
            agent.isStopped = true;
            return;
        }

        candidatos.Sort((a, b) => DistanciaXZ2(pos, a.transform.position).CompareTo(DistanciaXZ2(pos, b.transform.position)));

        // De los más cercanos, ir al primero que tenga camino de verdad (los otros se ignoran un rato)
        int n = Mathf.Min(candidatosPorBusqueda, candidatos.Count);
        for (int i = 0; i < n; i++)
        {
            Pilar p = candidatos[i];
            if (PuedeLlegar(p, out Vector3 destino))
            {
                IrAPilar(p, destino);
                return;
            }
            MarcarInalcanzable(p);
        }

        // Ninguno de estos tenía camino: el próximo frame prueba con los siguientes
        agent.isStopped = true;
    }

    /// <summary>
    /// ¿Hay un camino completo por el NavMesh hasta algún costado del pilar (lo bastante cerca para picarlo)?
    /// Prueba los 4 lados, empezando por el más cercano, así un pilar con agua adelante que se puede rodear sirve.
    /// </summary>
    private bool PuedeLlegar(Pilar p, out Vector3 destino)
    {
        destino = default;
        float alcance = rangoPicado + agent.radius;
        Vector3 pos = transform.position;
        float yPiso = pos.y - agent.baseOffset;
        Vector3 centro = p.transform.position;

        // Los 4 lados del pilar, ordenados del más cercano al más lejano al worker
        Vector3[] lados =
        {
            p.PuntoMasCercano(centro + Vector3.right * 10f),
            p.PuntoMasCercano(centro + Vector3.left * 10f),
            p.PuntoMasCercano(centro + Vector3.forward * 10f),
            p.PuntoMasCercano(centro + Vector3.back * 10f),
        };
        System.Array.Sort(lados, (a, b) => DistanciaXZ2(pos, a).CompareTo(DistanciaXZ2(pos, b)));

        foreach (Vector3 lado in lados)
        {
            Vector3 punto = new Vector3(lado.x, yPiso, lado.z);
            if (!NavMesh.SamplePosition(punto, out NavMeshHit hit, alcance, agent.areaMask)) continue; // ese lado es agua/otro pilar
            if (!agent.CalculatePath(hit.position, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;

            destino = hit.position;
            return true;
        }
        return false;
    }

    // ---------- Trabado ----------

    private void ReiniciarDeteccionTrabado()
    {
        posVentanaTrabado = transform.position;
        timerVentanaTrabado = 0f;
    }

    /// <summary>
    /// True si el camino no sirve, o si en los últimos 'tiempoMaximoTrabado' segundos casi no avanzó.
    /// Mira la posición real (no la velocidad), así detecta cuando el agente vibra empujando contra algo.
    /// </summary>
    private bool EstaTrabado()
    {
        if (!agent.isOnNavMesh) return true;
        if (agent.pathPending) return false;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;

        timerVentanaTrabado += Time.deltaTime;
        if (timerVentanaTrabado < tiempoMaximoTrabado) return false;

        bool trabado = DistanciaXZ(transform.position, posVentanaTrabado) < distanciaMinimaProgreso;
        ReiniciarDeteccionTrabado();
        return trabado;
    }

    private bool EsInalcanzable(Pilar p)
    {
        return inalcanzables.TryGetValue(p, out float hasta) && Time.time < hasta;
    }

    private void MarcarInalcanzable(Pilar p)
    {
        inalcanzables[p] = Time.time + segundosOlvidarInalcanzable;
    }

    private void OlvidarInalcanzablesViejos()
    {
        if (inalcanzables.Count == 0) return;
        paraOlvidar.Clear();
        foreach (var kv in inalcanzables)
            if (kv.Key == null || Time.time >= kv.Value) paraOlvidar.Add(kv.Key);
        foreach (Pilar p in paraOlvidar) inalcanzables.Remove(p);
    }

    private void IrAPilar(Pilar p, Vector3 destino)
    {
        objetivo = p;
        ReiniciarDeteccionTrabado();
        agent.isStopped = false;
        agent.SetDestination(destino);
        estado = Estado.Yendo;
    }

    private void Ir()
    {
        if (objetivo == null || objetivo.Consumido) { estado = Estado.Buscando; return; }

        if (EnRango(objetivo))
        {
            agent.isStopped = true;
            agent.ResetPath();
            timerGolpe = 0f;
            golpeSonado = false;
            estado = Estado.Picando;
            return;
        }

        // No avanza (o el camino ya no sirve) y no está en rango: probar otro pilar
        if (EstaTrabado())
        {
            MarcarInalcanzable(objetivo);
            objetivo = null;
            estado = Estado.Buscando;
        }
    }

    private void Picar()
    {
        if (objetivo == null || objetivo.Consumido) { estado = Estado.Buscando; return; }
        if (!EnRango(objetivo))
        {
            if (PuedeLlegar(objetivo, out Vector3 destino)) IrAPilar(objetivo, destino);
            else { MarcarInalcanzable(objetivo); objetivo = null; estado = Estado.Buscando; }
            return;
        }

        // Mirar hacia el pilar
        Vector3 dir = objetivo.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        // El sonido va al empezar cada golpe (no al terminarlo), así suena apenas empieza a picar
        if (!golpeSonado)
        {
            golpeSonado = true;
            if (AudioMaster.Instance != null)
                AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.cavar, transform.position);
        }

        timerGolpe += Time.deltaTime;

        // El pilar baja de forma continua mientras pica: llega abajo justo con el último golpe
        float vidaVisual = objetivo.Vida - danioPorGolpe * Mathf.Clamp01(timerGolpe / intervaloGolpe);
        objetivo.MostrarProgresoExcavacion(1f - vidaVisual / Mathf.Max(0.01f, objetivo.vidaMaxima));

        if (timerGolpe >= intervaloGolpe)
        {
            timerGolpe = 0f;
            golpeSonado = false; // el próximo golpe vuelve a sonar al empezar

            if (objetivo.Picar(danioPorGolpe))
            {
                // Terminó el pilar: ya está abajo, se elimina en el momento (sin animación pendiente)
                objetivo.TerminarHundimiento();
                // Consigue oro y descansa antes de llevarlo
                if (ResourceManager.Instance != null) ConseguirOro(ResourceManager.Instance.OroPorPilar());
                objetivo = null;
                timerCooldown = cooldownAlTerminar;
                estado = Estado.Descansando;
            }
        }
    }

    private void AnimarHundimientos()
    {
        for (int i = hundiendo.Count - 1; i >= 0; i--)
        {
            Pilar p = hundiendo[i];
            if (p == null || p.AvanzarHundimiento(Time.deltaTime))
                hundiendo.RemoveAt(i);
        }
    }

    private void Descansar()
    {
        timerCooldown -= Time.deltaTime;
        if (timerCooldown > 0f) return;

        if (oroCargado.Count > 0) BuscarAgua();
        else estado = Estado.Buscando;
    }

    // ---------- Lavar el oro ----------

    // Va al agua más cercana; si no hay (o no llega a ninguna), lleva el oro directo al centro
    private void BuscarAgua()
    {
        Whater masCercana = null;
        float mejor = float.MaxValue;
        Vector3 pos = transform.position;

        foreach (Whater w in Whater.Todos)
        {
            if (w == null || aguasInalcanzables.Contains(w)) continue;
            float d = DistanciaXZ(pos, w.PuntoMasCercano(pos));
            if (d < mejor) { mejor = d; masCercana = w; }
        }

        if (masCercana == null)
        {
            // No hay agua a la que llegar: se lleva igual, pero limpio para que la pila sea pareja
            aguasInalcanzables.Clear();
            LimpiarCarga();
            if (estado == Estado.Muerto) return;
            IrAEntregar();
            return;
        }

        aguaObjetivo = masCercana;
        agent.isStopped = false;
        Vector3 destino = masCercana.PuntoMasCercano(pos);
        destino.y = pos.y;
        agent.SetDestination(EnNavMesh(destino));
        ReiniciarDeteccionTrabado();
        estado = Estado.YendoAlAgua;
    }

    private void IrAlAgua()
    {
        if (aguaObjetivo == null) { BuscarAgua(); return; }

        if (DistanciaXZ(transform.position, aguaObjetivo.PuntoMasCercano(transform.position)) <= rangoPicado + agent.radius)
        {
            agent.isStopped = true;
            agent.ResetPath();
            timerLavado = ResourceManager.Instance != null ? ResourceManager.Instance.segundosLavadoOro : 3f;
            estado = Estado.Lavando;

            // Una sola vez, al empezar a lavar
            if (AudioMaster.Instance != null)
                AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.lavar, transform.position);
            return;
        }

        // No llega a esta agua: probar con otra
        if (EstaTrabado())
        {
            aguasInalcanzables.Add(aguaObjetivo);
            aguaObjetivo = null;
            BuscarAgua();
        }
    }

    private void Lavar()
    {
        if (aguaObjetivo != null)
        {
            // Mirar hacia el agua
            Vector3 dir = aguaObjetivo.PuntoMasCercano(transform.position) - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }
       
        timerLavado -= Time.deltaTime;
        if (timerLavado > 0f) return;

        aguaObjetivo = null;
        aguasInalcanzables.Clear();
        LimpiarCarga();
        if (estado == Estado.Muerto) return;
        IrAEntregar();
    }

    private void LimpiarCarga()
    {
        float chance = ResourceManager.Instance.toxicWaterCount / 20;
        if (chance > Random.Range(1,100))
        {
            ResourceManager.WorkerIsDead?.Invoke();
            HandleDead();
            return;
        }

        ResourceManager.GoldHasCleaned?.Invoke();
        Material limpio = ResourceManager.Instance != null ? ResourceManager.Instance.MaterialOroLimpio : null;
        foreach (GameObject oro in oroCargado) PintarOro(oro, limpio);
    }

    // ---------- Muerte ----------

    /// <summary>Se queda quieto, pierde el oro que cargaba y desaparece después de 'segundosMuerte'.</summary>
    public void HandleDead()
    {
        if (estado == Estado.Muerto) return;
        estado = Estado.Muerto;
        objetivo = null;
        aguaObjetivo = null;

        // Quieto
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Pierde el oro que llevaba encima (el que ya estaba volando llega igual a la pila)
        foreach (GameObject oro in oroCargado)
            if (oro != null) Destroy(oro);
        oroCargado.Clear();

        // Más adelante: acá va la animación de muerte (dura 'segundosMuerte')
        Destroy(gameObject, segundosMuerte);
    }

    private static void PintarOro(GameObject oro, Material mat)
    {
        if (oro == null || mat == null) return;
        var r = oro.GetComponentInChildren<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    // ---------- Oro ----------

    private void ConseguirOro(int cantidad)
    {
        var rm = ResourceManager.Instance;
        if (rm == null) { Debug.LogWarning("No hay ResourceManager en la escena.", this); return; }

        for (int i = 0; i < cantidad; i++)
        {
            GameObject oro = rm.CrearOro(transform.position);
            if (oro == null) return;
            SetFisica(oro, false);
            oro.transform.SetParent(transform, false);
            oro.transform.localPosition = puntoDeCarga + Vector3.up * (separacionCarga * oroCargado.Count);
            oro.transform.localRotation = Quaternion.identity;
            PintarOro(oro, rm.materialOroSucio);
            oroCargado.Add(oro);
        }
    }

    private void IrAEntregar()
    {
        agent.isStopped = false;
        Vector3 destino = PuntoEntrega();
        destino.y = transform.position.y;
        agent.SetDestination(EnNavMesh(destino));
        ReiniciarDeteccionTrabado();
        estado = Estado.LlevandoOro;
    }

    private void LlevarOro()
    {
        if (oroCargado.Count == 0) { estado = Estado.Buscando; return; }

        // Si no puede acercarse más (camino bloqueado), tira el oro desde donde está
        if (DistanciaXZ(transform.position, PuntoEntrega()) <= distanciaEntrega || EstaTrabado())
        {
            agent.isStopped = true;
            agent.ResetPath();
            timerTiro = 0f;
            // Cuanto más oro lleva, menos espera entre tiros (la curva y la duración de cada tiro siguen iguales)
            intervaloActual = Mathf.Min(intervaloTiro, tiempoMaximoDescarga / Mathf.Max(1, oroCargado.Count));
            estado = Estado.TirandoOro;
        }
    }

    private void TirarOro()
    {
        // Mirar hacia la pila
        Vector3 dir = PuntoEntrega() - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        timerTiro -= Time.deltaTime;
        if (timerTiro > 0f) return;

        if (oroCargado.Count == 0) { estado = Estado.Buscando; return; }

        // Puede tirar más de uno por frame si el intervalo es más corto que un frame
        while (timerTiro <= 0f && oroCargado.Count > 0)
        {
            TirarUno();
            timerTiro += intervaloActual;
        }
    }

    private void TirarUno()
    {
        // Tira el de arriba de la pila que lleva
        int ultimo = oroCargado.Count - 1;
        GameObject oro = oroCargado[ultimo];
        oroCargado.RemoveAt(ultimo);
        if (oro == null) return;

        // Vuelve al contenedor de oro del ResourceManager (para no quedar suelto en la jerarquía)
        Transform holder = ResourceManager.Instance != null ? ResourceManager.Instance.goldHolder : null;
        oro.transform.SetParent(holder, true);
        Vector2 r = Random.insideUnitCircle * radioPila;
        Vector3 hasta = PuntoEntrega() + new Vector3(r.x, 0f, r.y);
        hasta.y = AlturaPila(hasta);

        oroEnVuelo.Add(new OroEnVuelo { obj = oro, desde = oro.transform.position, hasta = hasta, t = 0f });
    }

    private void AnimarOroEnVuelo()
    {
        for (int i = oroEnVuelo.Count - 1; i >= 0; i--)
        {
            OroEnVuelo v = oroEnVuelo[i];
            if (v.obj == null) { oroEnVuelo.RemoveAt(i); continue; }

            v.t += Time.deltaTime / Mathf.Max(0.01f, duracionTiro);
            if (v.t >= 1f)
            {
                Aterrizar(v);
                oroEnVuelo.RemoveAt(i);
                continue;
            }

            // Curva: línea recta + parábola hacia arriba
            Vector3 pos = Vector3.Lerp(v.desde, v.hasta, v.t);
            pos.y += alturaTiro * 4f * v.t * (1f - v.t);
            v.obj.transform.position = pos;
        }
    }

    private void Aterrizar(OroEnVuelo v)
    {
        v.obj.transform.position = v.hasta;
        // Al caer recupera la física, así se acomoda solo en la pila
        SetFisica(v.obj, true);
        if (ResourceManager.Instance != null) ResourceManager.Instance.OroEntregado(v.obj);
    }

    private Vector3 PuntoEntrega()
    {
        return ResourceManager.Instance != null ? ResourceManager.Instance.puntoDeEntrega : Vector3.zero;
    }

    // Altura de la superficie (piso o pila de oro) en ese punto, para que el oro caiga encima
    private float AlturaPila(Vector3 punto)
    {
        Vector3 desde = new Vector3(punto.x, transform.position.y + 20f, punto.z);
        if (Physics.Raycast(desde, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
            return hit.point.y + 0.2f;
        return EnNavMesh(punto).y + 0.2f;
    }

    private static void SetFisica(GameObject obj, bool activa)
    {
        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!activa) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = !activa;
        }
    }

    private void AlConsumirsePilar(Pilar p)
    {
        // (los pilares sin camino se vuelven a probar solos después de 'segundosOlvidarInalcanzable')
        if (p == objetivo) { objetivo = null; estado = Estado.Buscando; }
    }

    // ---------- Texto de estado ----------

    /// <summary>Texto de qué está haciendo el worker (lo usa TipOnMouse).</summary>
    public string TextoEstado
    {
        get
        {
            switch (estado)
            {
                case Estado.Buscando:         return sinPilares ? "Sin pilares para picar" : "Buscando pilar...";
                case Estado.Yendo:            return "Yendo al pilar";
                case Estado.Picando:          return objetivo != null
                                                  ? $"Picando piedra ({Mathf.CeilToInt(objetivo.Vida)}/{Mathf.CeilToInt(objetivo.vidaMaxima)})"
                                                  : "Picando piedra";
                case Estado.Descansando:      return $"Descansando ({Mathf.Max(0f, timerCooldown):0.0}s)";
                case Estado.YendoAlAgua:      return $"Yendo a lavar el oro ({oroCargado.Count})";
                case Estado.Lavando:          return $"Lavando oro ({Mathf.Max(0f, timerLavado):0.0}s)";
                case Estado.LlevandoOro:      return $"Llevando oro ({oroCargado.Count})";
                case Estado.TirandoOro:       return "Dejando el oro";
                case Estado.Muerto:           return "Muerto";
                default:                      return "";
            }
        }
    }

    // ---------- Utilidades ----------

    private bool EnRango(Pilar p)
    {
        return DistanciaXZ(transform.position, p.PuntoMasCercano(transform.position)) <= rangoPicado + agent.radius;
    }

    private static float DistanciaXZ2(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static float DistanciaXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Vector3 EnNavMesh(Vector3 punto)
    {
        return NavMesh.SamplePosition(punto, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas) ? hit.position : punto;
    }
}

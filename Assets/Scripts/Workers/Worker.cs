using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Worker : MonoBehaviour
{
    private enum Estado { Buscando, Yendo, Picando, Descansando, LlevandoOro, TirandoOro }

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

    [Header("Debug")]
    [SerializeField] private Estado estado = Estado.Buscando;
    [SerializeField] private Pilar objetivo;

    private NavMeshAgent agent;
    private float timerGolpe;
    private float timerTrabado;
    private float timerCooldown;
    private bool sinPilares;
    private readonly HashSet<Pilar> inalcanzables = new HashSet<Pilar>();
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

        switch (estado)
        {
            case Estado.Buscando:         Buscar();          break;
            case Estado.Yendo:            Ir();              break;
            case Estado.Picando:          Picar();           break;
            case Estado.Descansando:      Descansar();       break;
            case Estado.LlevandoOro:      LlevarOro();       break;
            case Estado.TirandoOro:       TirarOro();        break;
        }

        AnimarHundimientos();
        AnimarOroEnVuelo();


      

    }

    // ---------- Picar piedra ----------

    private void Buscar()
    {
        Pilar masCercano = null;
        float mejor = float.MaxValue;
        Vector3 pos = transform.position;

        foreach (Pilar p in Pilar.Todos)
        {
            if (p == null || p.Consumido || inalcanzables.Contains(p)) continue;
            // Distancia al centro (barata) en vez de ClosestPoint, porque puede haber muchos pilares
            Vector3 dif = p.transform.position - pos;
            float d = dif.x * dif.x + dif.z * dif.z;
            if (d < mejor) { mejor = d; masCercano = p; }
        }

        sinPilares = masCercano == null;
        if (masCercano == null)
        {
            // Si descartamos todos, volver a intentar con todos la próxima vez
            if (inalcanzables.Count > 0) inalcanzables.Clear();
            agent.isStopped = true;
            return;
        }

        IrAPilar(masCercano);
    }

    private void IrAPilar(Pilar p)
    {
        objetivo = p;
        timerTrabado = 0f;
        agent.isStopped = false;
        Vector3 destino = p.PuntoMasCercano(transform.position);
        destino.y = transform.position.y;
        agent.SetDestination(EnNavMesh(destino));
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
            estado = Estado.Picando;
            return;
        }

        // Llegó al final del camino (o está quieto) y no está en rango: probar otro pilar
        bool quieto = !agent.pathPending &&
                      (agent.remainingDistance <= agent.stoppingDistance + 0.1f || agent.velocity.sqrMagnitude < 0.01f);
        timerTrabado = quieto ? timerTrabado + Time.deltaTime : 0f;

        if (timerTrabado >= tiempoMaximoTrabado)
        {
            inalcanzables.Add(objetivo);
            objetivo = null;
            estado = Estado.Buscando;
        }
    }

    private void Picar()
    {
        if (objetivo == null || objetivo.Consumido) { estado = Estado.Buscando; return; }
        if (!EnRango(objetivo)) { IrAPilar(objetivo); return; }

        // Mirar hacia el pilar
        Vector3 dir = objetivo.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        timerGolpe += Time.deltaTime;
        if (timerGolpe >= intervaloGolpe)
        {
            timerGolpe = 0f;
            if (!hundiendo.Contains(objetivo)) hundiendo.Add(objetivo);
            if (objetivo.Picar(danioPorGolpe))
            {
                // Terminó el pilar: consigue oro y descansa antes de llevarlo
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

        if (oroCargado.Count > 0) IrAEntregar();
        else estado = Estado.Buscando;
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
            oroCargado.Add(oro);
        }
    }

    private void IrAEntregar()
    {
        agent.isStopped = false;
        Vector3 destino = PuntoEntrega();
        destino.y = transform.position.y;
        agent.SetDestination(EnNavMesh(destino));
        timerTrabado = 0f;
        estado = Estado.LlevandoOro;
    }

    private void LlevarOro()
    {
        if (oroCargado.Count == 0) { estado = Estado.Buscando; return; }

        // Si no puede acercarse más (camino bloqueado), tira el oro desde donde está
        bool quieto = !agent.pathPending &&
                      (agent.remainingDistance <= agent.stoppingDistance + 0.1f || agent.velocity.sqrMagnitude < 0.01f);
        timerTrabado = quieto ? timerTrabado + Time.deltaTime : 0f;

        if (DistanciaXZ(transform.position, PuntoEntrega()) <= distanciaEntrega || timerTrabado >= tiempoMaximoTrabado)
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
        // Al desaparecer un pilar se puede abrir camino a otros que antes no alcanzaba
        inalcanzables.Clear();
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
                case Estado.LlevandoOro:      return $"Llevando oro ({oroCargado.Count})";
                case Estado.TirandoOro:       return "Dejando el oro";
                default:                      return "";
            }
        }
    }

    // ---------- Utilidades ----------

    private bool EnRango(Pilar p)
    {
        return DistanciaXZ(transform.position, p.PuntoMasCercano(transform.position)) <= rangoPicado + agent.radius;
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

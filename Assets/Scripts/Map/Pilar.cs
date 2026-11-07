using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pilar de piedra. No tiene Update: es pasivo y sólo cambia cuando un Worker lo pica
/// (Picar) o lo va hundiendo (AvanzarHundimiento). Al terminar de hundirse se desactiva y
/// el ResourceManager lo vuelve a activar y lo hace crecer (EmpezarCrecimiento / TerminarCrecimiento).
/// </summary>
[RequireComponent(typeof(Collider))]
public class Pilar : MonoBehaviour
{
    public BoneFinded boneObj;
    public bool isHaveABone = false;
    [Header("Piedra")]
    [Tooltip("Cuánta piedra hay que picar para consumir el pilar.")]
    public float vidaMaxima = 3f;
    [Tooltip("Velocidad a la que se hunde el pilar (unidades por segundo).")]
    public float velocidadBajada = 1f;

    [Header("Info (sólo para ver, se calcula al crearse)")]
    [Tooltip("Distancia en XZ al centro (punto de entrega del ResourceManager).")]
    [SerializeField] private float distanciaAlCentro;
    [Tooltip("Segundos aproximados que tarda en volver después de consumirse (sin la variación al azar).")]
    [SerializeField] private float segundosRegeneracionAprox;

    /// <summary>Evento global: cualquier script puede suscribirse con Pilar.PilarConsumido += MiMetodo;</summary>
    public static event Action<Pilar> PilarConsumido;

    /// <summary>
    /// Se dispara cuando se consume un pilar que tenía hueso.
    /// Uso: Pilar.HuesoEncontrado += MiMetodo;  (void MiMetodo(Pilar p) { ... })
    /// </summary>
    public static event Action<Pilar> HuesoEncontrado;

    /// <summary>Se dispara cuando un pilar terminó de crecer y está disponible de nuevo.</summary>
    public static event Action<Pilar> PilarRegenerado;

    /// <summary>Todos los pilares activos en la escena.</summary>
    public static readonly List<Pilar> Todos = new List<Pilar>();

    // ---------- Cooldown global de huesos ----------
    private static float huesosBloqueadosHasta = -1f;

    /// <summary>True mientras no pueden aparecer ni encontrarse huesos.</summary>
    public static bool HuesosEnCooldown => Time.time < huesosBloqueadosHasta;

    /// <summary>Segundos que faltan para que vuelvan los huesos (0 si no hay cooldown).</summary>
    public static float SegundosCooldownHuesos => Mathf.Max(0f, huesosBloqueadosHasta - Time.time);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstatico() { huesosBloqueadosHasta = -1f; }

    // Se encontró un hueso: bloquear huesos por un rato y sacar los que ya estaban en los pilares
    private static void IniciarCooldownHuesos()
    {
        float segundos = ResourceManager.Instance != null ? ResourceManager.Instance.cooldownHuesos : 30f;
        huesosBloqueadosHasta = Time.time + segundos;

        foreach (Pilar p in Todos)
        {
            if (!p.isHaveABone || p.huesoSuelto) continue; // los que ya se están excavando se cancelan en Eliminar
            p.isHaveABone = false;
            if (p.boneObj != null) p.boneObj.gameObject.SetActive(false);
        }
    }

    public float Vida { get; private set; }
    public bool Consumido { get; private set; }

    private Collider col;
    private Renderer rend;
    private Material materialOriginal;
    private UnityEngine.AI.NavMeshObstacle obstaculo;
    private float alturaInicial;
    private float alto;
    private Vector3 extentsColision;
    private int ultimoFrameHundido = -1;
    private bool eliminado;

    // Pose original del hueso dentro del pilar (para devolverlo después de revelarlo)
    private Vector3 huesoPosLocal;
    private Quaternion huesoRotLocal;
    private Vector3 huesoEscalaLocal;
    private bool huesoSuelto;
    private bool excavacionIniciada;

    void Awake()
    {
        col = GetComponent<Collider>();
        obstaculo = GetComponent<UnityEngine.AI.NavMeshObstacle>();
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) materialOriginal = rend.sharedMaterial;
        alturaInicial = transform.position.y;
        alto = rend != null ? rend.bounds.size.y : (col != null ? col.bounds.size.y : 1f);
        extentsColision = col != null ? col.bounds.extents : Vector3.one * 0.5f;
        Vida = vidaMaxima;

        if (boneObj != null)
        {
            huesoPosLocal = boneObj.transform.localPosition;
            huesoRotLocal = boneObj.transform.localRotation;
            huesoEscalaLocal = boneObj.transform.localScale;
        }
    }

    void OnDestroy()
    {
        // Si el pilar se destruye (ej. al regenerar el mapa) con el hueso suelto, no dejarlo huérfano
        if (huesoSuelto && boneObj != null && gameObject.scene.isLoaded) Destroy(boneObj.gameObject);
    }

    void Start()
    {
        HaveABone();

        var rm = ResourceManager.Instance;
        distanciaAlCentro = rm != null ? rm.DistanciaAlCentro(transform.position)
                                       : new Vector2(transform.position.x, transform.position.z).magnitude;
        if (rm != null) segundosRegeneracionAprox = rm.SegundosRegeneracionBase(distanciaAlCentro);
    }

    // Al crearse y al volver a crecer: sin hueso (la chance se tira recién al empezar a excavar)
    public void HaveABone()
    {
        excavacionIniciada = false;
        if (huesoSuelto) return; // el hueso anterior todavía se está revelando
        isHaveABone = false;
        if (boneObj != null) boneObj.gameObject.SetActive(false);
    }

    // Primer golpe: tirar la chance de hueso. En cooldown, seguro que no tiene.
    private void TirarChanceHueso()
    {
        excavacionIniciada = true;
        if (huesoSuelto) return;
        isHaveABone = !HuesosEnCooldown && UnityEngine.Random.Range(0, 10) == 5;
        if (boneObj != null) boneObj.gameObject.SetActive(isHaveABone);
    }

    private void SoltarHueso()
    {
        if (huesoSuelto || boneObj == null) return;
        huesoSuelto = true;
        boneObj.transform.SetParent(transform.parent, true);
    }

    // Después de revelarlo: vuelve adentro del pilar, en su pose original, oculto y esperando el próximo respawn
    private void RecuperarHueso()
    {
        huesoSuelto = false;
        if (boneObj == null) return;
        boneObj.transform.SetParent(transform, false);
        boneObj.transform.localPosition = huesoPosLocal;
        boneObj.transform.localRotation = huesoRotLocal;
        boneObj.transform.localScale = huesoEscalaLocal;
        boneObj.gameObject.SetActive(false); // último: cuando el pilar vuelva a crecer, HaveABone decide si se ve
    }

    /// <summary>Distancia en XZ al centro (punto de entrega).</summary>
    public float DistanciaAlCentro => distanciaAlCentro;

    void OnEnable()  { Todos.Add(this); }
    void OnDisable() { Todos.Remove(this); }

    /// <summary>Le saca piedra al pilar. Devuelve true si con este golpe quedó consumido.</summary>
    public bool Picar(float cantidad)
    {
        if (Consumido) return false;
        if (!excavacionIniciada) TirarChanceHueso();
        if (isHaveABone) SoltarHueso();
        Vida -= cantidad;
        if (Vida <= 0f)
        {
            Vida = 0f;
            Consumido = true;
            // Deja de bloquear el NavMesh y de recibir raycasts; se termina de hundir con AvanzarHundimiento
            if (col != null) col.enabled = false;
            if (obstaculo != null) obstaculo.enabled = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Mueve el pilar hacia la altura que le corresponde según la piedra que le queda.
    /// Lo llama el Worker cada frame mientras el pilar se está hundiendo.
    /// Devuelve true cuando ya no hace falta seguir llamándolo (llegó a su altura, o se eliminó).
    /// </summary>
    public bool AvanzarHundimiento(float deltaTime)
    {
        if (eliminado) return true;

        // Si varios workers lo hunden, avanzar sólo una vez por frame
        if (ultimoFrameHundido == Time.frameCount) return false;
        ultimoFrameHundido = Time.frameCount;

        float yObjetivo = AlturaObjetivo();
        Vector3 pos = transform.position;
        pos.y = Mathf.MoveTowards(pos.y, yObjetivo, velocidadBajada * deltaTime);
        transform.position = pos;

        if (!Mathf.Approximately(pos.y, yObjetivo)) return false;

        if (Consumido) Eliminar();
        return true;
    }

    /// <summary>
    /// Baja el pilar según el progreso de excavación (0 = entero, 1 = abajo del todo).
    /// Lo llama el Worker cada frame mientras pica, así llega abajo justo con el último golpe.
    /// Sólo baja, nunca sube (por si dos workers lo pican a la vez).
    /// </summary>
    public void MostrarProgresoExcavacion(float progreso)
    {
        if (eliminado) return;
        float y = alturaInicial - alto * Mathf.Clamp01(progreso);
        Vector3 pos = transform.position;
        if (y >= pos.y) return;
        pos.y = y;
        transform.position = pos;
    }

    /// <summary>Lleva el pilar directo a su altura final (por ejemplo si el worker que lo hundía desaparece).</summary>
    public void TerminarHundimiento()
    {
        if (eliminado) return;
        Vector3 pos = transform.position;
        pos.y = AlturaObjetivo();
        transform.position = pos;
        if (Consumido) Eliminar();
    }

    /// <summary>Punto del pilar más cercano a una posición (para medir si el worker está al lado).</summary>
    public Vector3 PuntoMasCercano(Vector3 desde)
    {
        return col != null && col.enabled ? col.ClosestPoint(desde) : transform.position;
    }

    private float AlturaObjetivo()
    {
        float progreso = vidaMaxima > 0f ? 1f - Mathf.Clamp01(Vida / vidaMaxima) : 1f;
        return alturaInicial - alto * progreso;
    }

    private void Eliminar()
    {
        eliminado = true;
        PilarConsumido?.Invoke(this);

        // Si tenía hueso: sube y desaparece, y recién ahí se avisa que se encontró
        if (isHaveABone)
        {
            isHaveABone = false;

            if (HuesosEnCooldown)
            {
                // Se encontró otro hueso hace poco: este se pierde
                if (huesoSuelto) RecuperarHueso();
                else if (boneObj != null) boneObj.gameObject.SetActive(false);
            }
            else if (boneObj != null)
            {
                SoltarHueso();
                boneObj.Revelar(() =>
                {
                    RecuperarHueso();
                    // Si otro hueso se reveló al mismo tiempo y ya inició el cooldown, este no cuenta
                    if (HuesosEnCooldown) return;
                    IniciarCooldownHuesos();
                    HuesoEncontrado?.Invoke(this);
                });
            }
            else
            {
                IniciarCooldownHuesos();
                HuesoEncontrado?.Invoke(this);
            }
        }

        // Se desactiva y el ResourceManager lo reactiva más tarde
        if (ResourceManager.Instance != null) ResourceManager.Instance.RegistrarPilarConsumido(this);
        else Destroy(gameObject);
    }

    /// <summary>¿Hay un worker parado donde aparecería el pilar? (para no aparecer encima de él)</summary>
    public bool LugarOcupado()
    {
        Vector3 centro = transform.position;
        centro.y = alturaInicial;
        var hits = Physics.OverlapBox(centro, extentsColision, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.GetComponentInParent<Worker>() != null) return true;
        return false;
    }

    // ---------- Regeneración (la maneja el ResourceManager) ----------

    /// <summary>Reaparece hundido y con otro material. Sigue "Consumido" (los workers no lo eligen) hasta TerminarCrecimiento.</summary>
    public void EmpezarCrecimiento(Material materialCreciendo)
    {
        Vida = 0f;
        Consumido = true;
        eliminado = false;
        ultimoFrameHundido = -1;

        AvanzarCrecimiento(0f);

        if (rend != null)
        {
            if (materialOriginal == null) materialOriginal = rend.sharedMaterial;
            if (materialCreciendo != null) rend.sharedMaterial = materialCreciendo;
        }

        // Ya bloquea el paso mientras crece, pero sin collider (no se puede apuntar ni recibe el oro)
        if (col != null) col.enabled = false;
        if (obstaculo != null) obstaculo.enabled = true;

        gameObject.SetActive(true);
    }

    /// <summary>Sube el pilar según el progreso (0 = abajo del todo, 1 = altura completa).</summary>
    public void AvanzarCrecimiento(float progreso)
    {
        Vector3 pos = transform.position;
        pos.y = alturaInicial - alto * (1f - Mathf.Clamp01(progreso));
        transform.position = pos;
    }

    /// <summary>Terminó de crecer: material normal, vida llena y disponible para los workers.</summary>
    public void TerminarCrecimiento()
    {
        AvanzarCrecimiento(1f);
        if (rend != null && materialOriginal != null) rend.sharedMaterial = materialOriginal;

        Vida = vidaMaxima;
        Consumido = false;
        if (col != null) col.enabled = true;
        if (obstaculo != null) obstaculo.enabled = true;

        HaveABone();
        PilarRegenerado?.Invoke(this);
    }
}

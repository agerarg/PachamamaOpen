using UnityEngine;

public class ShowInfo : MonoBehaviour, ToolTipInUi
{
    public InfoType info = InfoType.none;
    public enum InfoType
    {
        none,
       gold, 
       workers,
       huesos, 
       toxinas,
    }

    public string ShowText()
    {
        switch(info)
        {
            case InfoType.none:
                return "No hay información disponible";
            case InfoType.gold:
                return "El oro es un recurso valioso que se utiliza<br> para comprar mejoras y objetos en el juego.";
            case InfoType.workers:
                return "Los trabajadores son necesarios para recolectar recursos.";
            case InfoType.huesos:
                return "Los huesos son mejoras permantentes a elección.";
            case InfoType.toxinas:
                float chance = ResourceManager.Instance.toxicWaterCount / 20f;
                return "Grado de contaminacion de agua,<br> ahora mismo hay un " + chance.ToString("F0") + "% chance de que muera un trabajador.";
            default:
                return "Información desconocida";
        }

    }
}

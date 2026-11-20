using UnityEngine;

namespace Arlen.Items
{
    /// <summary>
    /// Presentation only: tints and resizes the placeholder blade to match the equipped weapon. Swap this
    /// for a component that swaps weapon meshes once Blender models exist.
    /// </summary>
    public class EquipmentVisuals : MonoBehaviour
    {
        [SerializeField] PlayerEquipment equipment;
        [SerializeField] Transform blade;
        [SerializeField] Renderer bladeRenderer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        MaterialPropertyBlock block;
        Vector3 baseScale, basePosition;
        bool captured;

        void Awake()
        {
            if (equipment == null) equipment = GetComponentInParent<PlayerEquipment>();
            Capture();
        }

        void Capture()
        {
            if (captured || blade == null) return;
            captured = true;
            baseScale = blade.localScale;
            basePosition = blade.localPosition;
        }

        void OnEnable()
        {
            if (equipment != null) equipment.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (equipment != null) equipment.Changed -= Refresh;
        }

        void Refresh()
        {
            if (equipment == null || blade == null) return;
            Capture();
            var weapon = equipment.Weapon;
            blade.gameObject.SetActive(weapon != null);
            if (weapon == null) return;

            // The blade is a cube scaled along local Z, starting at the guard; keep its base where it is.
            float length = baseScale.z * weapon.bladeLengthScale;
            float start = basePosition.z - baseScale.z * 0.5f;
            blade.localScale = new Vector3(baseScale.x, baseScale.y, length);
            blade.localPosition = new Vector3(basePosition.x, basePosition.y, start + length * 0.5f);

            if (bladeRenderer == null) return;
            block ??= new MaterialPropertyBlock();
            bladeRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, weapon.bladeColor);
            block.SetColor(ColorId, weapon.bladeColor);
            bladeRenderer.SetPropertyBlock(block);
        }
    }
}

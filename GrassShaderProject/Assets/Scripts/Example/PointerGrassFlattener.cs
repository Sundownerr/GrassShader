using UnityEngine;

namespace Grass
{
    public class PointerGrassFlattener : GrassFlattener
    {
        private Camera _camera;
        [SerializeField] private GrassGPUInstancing[] _grass;

        private void Start()
        {
            _camera = Camera.main;

            foreach (var grassGPUInstancing in _grass)
            {
                grassGPUInstancing.AddGrassFlattener(this);
            }

        }

        private void Update()
        {
            if (!Input.GetMouseButton(1))
            {
                return;
            }

            var ray = _camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out var hitInfo, 1000))
            {
                transform.position = hitInfo.point;
            }
        }
    }
}
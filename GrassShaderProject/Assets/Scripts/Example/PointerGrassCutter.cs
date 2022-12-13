using UnityEngine;

namespace Grass
{
    public class PointerGrassCutter : GrassCutter
    {
        private Camera _camera;
        [SerializeField] private GrassGPUInstancing[] _grass;

        private void Start()
        {
            _camera = Camera.main;
            foreach (var grassGPUInstancing in _grass)
            {
                grassGPUInstancing.AddGrassCutter(this);
            }
           
        }

        private void Update()
        {
            if (!Input.GetMouseButton(0))
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
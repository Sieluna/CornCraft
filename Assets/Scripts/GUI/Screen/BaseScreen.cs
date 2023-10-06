#nullable enable
using UnityEngine;

namespace CraftSharp.UI
{
    public abstract class BaseScreen : MonoBehaviour
    {
        protected static readonly int SHOW = Animator.StringToHash("Show");

        public abstract bool IsActive { get; set; }

        public abstract bool ReleaseCursor();
        public abstract bool ShouldPause();

        protected bool initialized;
        protected abstract bool Initialize();

        public virtual bool EnsureInitialized()
        {
            if (!initialized)
                return (initialized = Initialize());
            return true;
        }

        protected virtual void Start()
        {
            EnsureInitialized();
        }

    }
}
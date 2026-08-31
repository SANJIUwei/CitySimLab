namespace CitySim.UnityMock
{
    public abstract class MonoBehaviourLike
    {
        public bool IsInitialized { get; private set; }

        public void Init()
        {
            if (IsInitialized)
            {
                return;
            }

            OnInit();
            IsInitialized = true;
        }

        public void Update(float deltaTime)
        {
            if (!IsInitialized)
            {
                return;
            }

            OnUpdate(deltaTime);
        }

        protected virtual void OnInit()
        {
        }

        protected virtual void OnUpdate(float deltaTime)
        {
        }
    }
}

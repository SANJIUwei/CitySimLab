namespace CitySim.UnityMock
{
    public sealed class UnityTime
    {
        public float DeltaTime { get; private set; }

        public float Time { get; private set; }

        public void Advance(float deltaTime)
        {
            if (deltaTime < 0)
            {
                deltaTime = 0;
            }

            DeltaTime = deltaTime;
            Time += deltaTime;
        }
    }
}

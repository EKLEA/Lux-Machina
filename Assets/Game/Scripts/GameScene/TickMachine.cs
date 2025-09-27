    using UnityEngine;
    using Zenject;

    public class TickMachine : ITickable, ILateTickable, IFixedTickable
    {
        private readonly SignalBus _signalBus;
        private int _tickCount;
        public bool pause;

        public TickMachine(SignalBus signalBus)
        {
            _signalBus = signalBus;
            pause=true;
        }
        public void FixedTick()
        {
            if (!pause)
            {
                
            }
        }

        public void LateTick()
        {
            if (!pause)
            {
            
            }
        }

        public void Tick()
        {
            if (!pause)
            {
                _tickCount++;

                _signalBus.Fire(new TickableEvent
                {
                    TickCount = _tickCount,
                    DeltaTime = Time.deltaTime
                });
            }
        }
    }
        public struct TickableEvent
        {
            public int TickCount;
            public float DeltaTime;
        }
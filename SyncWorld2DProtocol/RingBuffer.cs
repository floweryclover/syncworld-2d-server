using System;

namespace SyncWorld2DProtocol
{
    public class RingBuffer
    {
        public int Capacity => _buffer.Length - 1;
        public int WritableTotalSize => (_buffer.Length + _tail - _head - 1) % _buffer.Length;
        public int WritableOnceSize
        {
            get
            {
                if (_head >= _tail)
                {
                    var returnValue = _buffer.Length - 1 - _head;
                    if (_tail > 0)
                    {
                        returnValue += 1;
                    }
                    return returnValue;
                }
                else
                {
                    return _tail - _head - 1;
                }
            }
        }
        public Span<byte> WritableOnceSpan => new Span<byte>(_buffer, _head, WritableOnceSize);
        public ArraySegment<byte> WritableOnceArraySegment => new ArraySegment<byte>(_buffer, _head, WritableOnceSize);
        public int ReadableTotalSize => (_buffer.Length + _head - _tail) % _buffer.Length;
        public int ReadableOnceSize
        {
            get
            {
                if (_head >= _tail)
                {
                    return _head - _tail;
                }
                else
                {
                    return (_buffer.Length - 1) - _tail + 1;
                }
            }
        }
        public ReadOnlySpan<byte> ReadableOnceSpan => new ReadOnlySpan<byte>(_buffer, _tail, ReadableOnceSize);
        public ArraySegment<byte> ReadableOnceArraySegment => new ArraySegment<byte>(_buffer, _tail, ReadableOnceSize);
        private byte[] _buffer;
        private byte[] _temporaryContiguousBuffer; // RingBuffer의 불연속적인 부분에 읽고자 하는 데이터가 걸쳐있을 경우에 임시로 쓰이는 버퍼
        private int _head; // 데이터를 포함하지 않음
        private int _tail; // _head != _tail일 경우 데이터를 포함함
        public RingBuffer(int capacity)
        {
            _buffer = new byte[capacity];
            _temporaryContiguousBuffer = new byte[Protocol.MaxMessageSize];
            _head = _tail = 0;
        }

        public void Clear()
        {
            _head = _tail = 0;
        }

        public ReadOnlySpan<byte> Pop(int size) => PeekOrPop(size, true);
        public ReadOnlySpan<byte> Peek(int size) => PeekOrPop(size, false);
        private ReadOnlySpan<byte> PeekOrPop(int size, bool isPopping)
        {
            if (ReadableTotalSize < size)
            {
                throw new InvalidOperationException($"링버퍼에서 {size}bytes를 읽으려고 시도했으나 {ReadableTotalSize}만 읽을 수 있습니다.");
            }

            ReadOnlySpan<byte> returnValue;
            if (size > ReadableOnceSize)
            {
                Buffer.BlockCopy(_buffer, _tail, _temporaryContiguousBuffer, 0, ReadableOnceSize);
                Buffer.BlockCopy(_buffer, 0, _temporaryContiguousBuffer, ReadableOnceSize, size - ReadableOnceSize);
                returnValue = new ReadOnlySpan<byte>(_temporaryContiguousBuffer, 0, size);
            }
            else
            {
                returnValue = new ReadOnlySpan<byte>(_buffer, _tail, size);
            }

            if (isPopping)
            {
                UpdateRead(size);
            }

            return returnValue;
        }

        public void Write(byte[] bytes, int size)
        {
            if (size > WritableTotalSize)
            {
                throw new InvalidOperationException($"링버퍼의 여유 크기가 원본 데이터 크기보다 작습니다: (여유 {WritableTotalSize}, 데이터 크기 {size})");
            }

            if (size > WritableOnceSize)
            {
                Buffer.BlockCopy(bytes, 0, _buffer, _head, WritableOnceSize);
                Buffer.BlockCopy(bytes, WritableOnceSize, _buffer, 0, size - WritableOnceSize);
            }
            else
            {
                Buffer.BlockCopy(bytes, 0, _buffer, _head, size);
            }
            UpdateWritten(size);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public void UpdateWritten(int amount) => MoveArm(amount, ref _head, WritableTotalSize);
       
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public void UpdateRead(int amount) => MoveArm(amount, ref _tail, ReadableTotalSize);

        private void MoveArm(int amount, ref int arm, int compareWith)
        {
            if (amount > compareWith)
            {
                throw new InvalidOperationException();
            }
            arm = (arm + amount) % _buffer.Length;
        }
    }
}

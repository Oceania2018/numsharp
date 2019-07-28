﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NumSharp.Backends.Unmanaged;
using NumSharp.Utilities;

namespace NumSharp.Backends
{
    public unsafe partial class NDIterator<TOut> : NDIterator, IEnumerable<TOut>, IDisposable where TOut : unmanaged
    {
        private int index;
        public readonly IMemoryBlock Block;
        public readonly IteratorType Type;
        public Shape Shape; //TODO! is there a performance difference if this shape is readonly or not?
        public Shape? BroadcastedShape; //TODO! is there a performance difference if this shape is readonly or not?
        public bool AutoReset;

        public Func<TOut> MoveNext;
        public MoveNextReferencedDelegate<TOut> MoveNextReference;
        public Func<bool> HasNext;
        public Action Reset;

        public NDIterator(IMemoryBlock block, Shape shape, Shape? broadcastedShape, bool autoReset = false)
        {
            if (shape.IsEmpty || shape.size == 0)
                throw new InvalidOperationException("Can't construct NDIterator with an empty shape.");

            Block = block ?? throw new ArgumentNullException(nameof(block));
            Shape = shape;
            BroadcastedShape = broadcastedShape;
            if (broadcastedShape.HasValue && shape.size != broadcastedShape.Value.size)
                AutoReset = true;
            else
                AutoReset = autoReset;

            if (shape.IsScalar)
                Type = IteratorType.Scalar;
            else if (shape.NDim == 1)
                Type = IteratorType.Vector;
            else if (shape.NDim == 2)
                Type = IteratorType.Matrix;
            else
                Type = IteratorType.Tensor;

            SetDefaults();
        }

        public NDIterator(IArraySlice slice, Shape shape, Shape? broadcastedShape, bool autoReset = false) : this((IMemoryBlock)slice, shape, broadcastedShape, autoReset) { }

        public NDIterator(UnmanagedStorage storage, bool autoReset = false) : this((IMemoryBlock)storage?.InternalArray, storage?.Shape ?? default, null, autoReset) { }

        public NDIterator(NDArray arr, bool autoReset = false) : this(arr?.Storage, autoReset) { }

        /// <summary>
        ///     Set the mode according to given parameters
        /// </summary>
        /// <param name="autoreset">The iterator will transparently reset after it is done.</param>
        /// <param name="reshape">Provide a different shape to the iterator.</param>
        public void SetMode(bool autoreset, Shape reshape = default)
        {
            AutoReset = autoreset;
            if (!reshape.IsEmpty)
                Shape = reshape;

            SetDefaults();
        }

        protected void SetDefaults()
        {

#if _REGEN
            #region Compute
		    switch (Block.TypeCode)
		    {
			    %foreach supported_currently_supported,supported_currently_supported_lowercase%
			    case NPTypeCode.#1: setDefaults_#1(); break;
			    %
			    default:
				    throw new NotSupportedException();
		    }
            #endregion
#else
            #region Compute
		    switch (Block.TypeCode)
		    {
			    case NPTypeCode.Boolean: setDefaults_Boolean(); break;
			    case NPTypeCode.Byte: setDefaults_Byte(); break;
			    case NPTypeCode.Int16: setDefaults_Int16(); break;
			    case NPTypeCode.UInt16: setDefaults_UInt16(); break;
			    case NPTypeCode.Int32: setDefaults_Int32(); break;
			    case NPTypeCode.UInt32: setDefaults_UInt32(); break;
			    case NPTypeCode.Int64: setDefaults_Int64(); break;
			    case NPTypeCode.UInt64: setDefaults_UInt64(); break;
			    case NPTypeCode.Char: setDefaults_Char(); break;
			    case NPTypeCode.Double: setDefaults_Double(); break;
			    case NPTypeCode.Single: setDefaults_Single(); break;
			    case NPTypeCode.Decimal: setDefaults_Decimal(); break;
			    default:
				    throw new NotSupportedException();
		    }
            #endregion
#endif

        }

        protected void setDefaults_NoCast()
        {
            if (AutoReset)
            {
                autoresetDefault_NoCast();
                return;
            }

            //non auto-resetting.
            var localBlock = Block;
            Shape shape = Shape;
            if (Shape.IsSliced)
            {
                //Shape is sliced, not auto-resetting
                switch (Type)
                {
                    case IteratorType.Scalar:
                    {
                        var hasNext = new Reference<bool>(true);
                        var offset = shape.TransformOffset(0);
                        if (offset != 0)
                        {
                            MoveNext = () =>
                            {
                                hasNext.Value = false;
                                return *((TOut*)localBlock.Address + offset);
                            };
                            MoveNextReference = () =>
                            {
                                hasNext.Value = false;
                                return ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address + offset);
                            };
                        }
                        else
                        {
                            MoveNext = () =>
                            {
                                hasNext.Value = false;
                                return *((TOut*)localBlock.Address);
                            };
                            MoveNextReference = () =>
                            {
                                hasNext.Value = false;
                                return ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address);
                            };
                        }

                        Reset = () => hasNext.Value = true;
                        HasNext = () => hasNext.Value;
                        break;
                    }

                    case IteratorType.Vector:
                    {
                        MoveNext = () => *((TOut*)localBlock.Address + shape.GetOffset(index++));
                        MoveNextReference = () => ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address + shape.GetOffset(index++));
                        Reset = () => index = 0;
                        HasNext = () => index < Shape.size;
                        break;
                    }

                    case IteratorType.Matrix:
                    case IteratorType.Tensor:
                    {
                        var hasNext = new Reference<bool>(true);
                        var iterator = new NDCoordinatesIncrementor(ref shape, _ => hasNext.Value = false);
                        Func<int[], int> getOffset = shape.GetOffset;
                        var index = iterator.Index;

                        MoveNext = () =>
                        {
                            var ret = *((TOut*)localBlock.Address + getOffset(index));
                            iterator.Next();
                            return ret;
                        };
                        MoveNextReference = () =>
                        {
                            ref var ret = ref Unsafe.AsRef<TOut>(((TOut*)localBlock.Address + getOffset(index)));
                            iterator.Next();
                            return ref ret;
                        };

                        Reset = () =>
                        {
                            iterator.Reset();
                            hasNext.Value = true;
                        };

                        HasNext = () => hasNext.Value;
                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                //Shape is not sliced, not auto-resetting
                switch (Type)
                {
                    case IteratorType.Scalar:
                        var hasNext = new Reference<bool>(true);
                        MoveNext = () =>
                        {
                            hasNext.Value = false;
                            return *((TOut*)localBlock.Address);
                        };
                        MoveNextReference = () =>
                        {
                            hasNext.Value = false;
                            return ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address);
                        };
                        Reset = () => hasNext.Value = true;
                        HasNext = () => hasNext.Value;
                        break;

                    case IteratorType.Vector:
                        MoveNext = () => *((TOut*)localBlock.Address + index++);
                        MoveNextReference = () => ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address + index++);
                        Reset = () => index = 0;
                        HasNext = () => index < Shape.size;
                        break;

                    case IteratorType.Matrix:
                    case IteratorType.Tensor:
                        var iterator = new NDOffsetIncrementor(Shape.dimensions, Shape.strides); //we do not copy the dimensions because there is not risk for the iterator's shape to change.
                        MoveNext = () => *((TOut*)localBlock.Address + iterator.Next());
                        MoveNextReference = () => ref Unsafe.AsRef<TOut>(((TOut*)localBlock.Address + iterator.Next()));
                        Reset = () => iterator.Reset();
                        HasNext = () => iterator.HasNext;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        protected void autoresetDefault_NoCast()
        {
            var localBlock = Block;
            Shape shape = Shape;
            if (Shape.IsSliced)
            {
                //Shape is sliced, auto-resetting
                switch (Type)
                {
                    case IteratorType.Scalar:
                    {
                        var offset = shape.TransformOffset(0);
                        if (offset != 0)
                        {
                            MoveNext = () => *((TOut*)localBlock.Address + offset);
                            MoveNextReference = () => ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address + offset);
                        }
                        else
                        {
                            MoveNext = () => *((TOut*)localBlock.Address);
                            MoveNextReference = () => ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address);
                        }

                        Reset = () => { };
                        HasNext = () => true;
                        break;
                    }

                    case IteratorType.Vector:
                    {
                        var size = Shape.size;
                        MoveNext = () =>
                        {
                            var ret = *((TOut*)localBlock.Address + shape.GetOffset(index++));
                            if (index >= size)
                                index = 0;
                            return ret;
                        };
                        MoveNextReference = () =>
                        {
                            ref var ret = ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address + shape.GetOffset(index++));
                            if (index >= size)
                                index = 0;
                            return ref ret;
                        };
                            Reset = () => index = 0;
                        HasNext = () => true;
                        break;
                    }

                    case IteratorType.Matrix:
                    case IteratorType.Tensor:
                    {
                        var iterator = new NDCoordinatesIncrementor(ref shape, incr => incr.Reset());
                        var index = iterator.Index;
                        Func<int[], int> getOffset = shape.GetOffset;
                        MoveNext = () =>
                        {
                            var ret = *((TOut*)localBlock.Address + getOffset(index));
                            iterator.Next();
                            return ret;
                        };
                        MoveNextReference = () =>
                        {
                            ref var ret = ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address + getOffset(iterator.Next()));
                            iterator.Next();
                            return ref ret;
                        };
                        Reset = () => iterator.Reset();
                        HasNext = () => true;
                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                //Shape is not sliced, auto-resetting
                switch (Type)
                {
                    case IteratorType.Scalar:
                        MoveNext = () => *(TOut*)localBlock.Address;
                        MoveNextReference = () => ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address);
                        Reset = () => { };
                        HasNext = () => true;
                        break;
                    case IteratorType.Vector:
                        var size = Shape.size;
                        MoveNext = () =>
                        {
                            var ret = *((TOut*)localBlock.Address + (index++));
                            if (index >= size)
                                index = 0;
                            return ret;
                        };
                        MoveNextReference = () =>
                        {
                            ref var ret = ref Unsafe.AsRef<TOut>((TOut*)localBlock.Address + (index++));
                            if (index >= size)
                                index = 0;
                            return ref ret;
                        };
                        Reset = () => index = 0;
                        HasNext = () => true;
                        break;
                    case IteratorType.Matrix:
                    case IteratorType.Tensor:
                        var iterator = new NDOffsetIncrementorAutoresetting(Shape.dimensions, Shape.strides); //we do not copy the dimensions because there is not risk for the iterator's shape to change.
                        MoveNext = () => *((TOut*)localBlock.Address + iterator.Next());
                        MoveNextReference = () => ref Unsafe.AsRef<TOut>(((TOut*)localBlock.Address + iterator.Next()));
                        HasNext = () => true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            //incase of a cross-reference
            MoveNext = null;
            Reset = null;
            HasNext = null;
        }


        /// <summary>Returns an enumerator that iterates through the collection.</summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<TOut> GetEnumerator()
        {
            var next = MoveNext;
            var hasNext = HasNext;

            while (hasNext())
                yield return next();

            yield break;
        }

        #region Implicit Implementations

        /// <summary>Returns an enumerator that iterates through a collection.</summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator"></see> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        IMemoryBlock NDIterator.Block => Block;

        IteratorType NDIterator.Type => Type;

        Shape NDIterator.Shape => Shape;

        Shape? NDIterator.BroadcastedShape => BroadcastedShape;

        bool NDIterator.AutoReset => AutoReset;

        Func<T1> NDIterator.MoveNext<T1>() => (Func<T1>)(object)MoveNext;

        MoveNextReferencedDelegate<T1> NDIterator.MoveNextReference<T1>() => (MoveNextReferencedDelegate<T1>)(object)MoveNextReference;

        Func<bool> NDIterator.HasNext => HasNext;

        Action NDIterator.Reset => Reset;

        #endregion
    }
}
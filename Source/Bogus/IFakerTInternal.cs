using System;

namespace Bogus
{
   /// <summary>
   /// Hidden API implemented explicitly on <see cref="Faker{T}"/>. When <see cref="Faker{T}"/> is casted explicitly to <see cref="IFakerTInternal"/>,
   /// the cast reveals some protected internal objects of <see cref="Faker{T}"/> without needing to derive
   /// from <see cref="Faker{T}"/>. This is useful for extensions methods that need access internal variables of <see cref="Faker{T}"/> like <see cref="Faker"/>, <see cref="IBinder"/>, <see cref="LocalSeed"/>, and type of T.
   /// </summary>
   public interface IFakerTInternal
   {
      /// <summary>
      /// The internal FakerHub object that is used in f => f rules. Usually used to gain access to a source of randomness by extension methods.
      /// </summary>
      Faker FakerHub { get; }

      /// <summary>
      /// The field/property binder used by <see cref="Faker{T}"/>.
      /// </summary>
      IBinder Binder { get; }

      /// <summary>
      /// The local seed of <see cref="Faker{T}"/> if available. Null local seed means the Global <see cref="Randomizer.Seed"/> property is being used.
      /// </summary>
      int? LocalSeed { get; }

      /// <summary>
      /// The type of T in <see cref="Faker{T}"/>.
      /// </summary>
      Type TypeOfT { get; }
   }
}
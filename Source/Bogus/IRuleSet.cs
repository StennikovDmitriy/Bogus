using System;
using System.Linq.Expressions;

namespace Bogus
{
   /// <summary>
   /// An interface for defining a set of rules.
   /// </summary>
   public interface IRuleSet<T, out TFaker> where T : class
   {
      /// <summary>
      /// Creates a rule for a compound property and providing access to the instance being generated.
      /// </summary>
      TFaker RuleFor<TProperty>(Expression<Func<T, TProperty>> property, Func<Faker, T, TProperty> setter);

      /// <summary>
      /// Creates a rule for a property.
      /// </summary>
      TFaker RuleFor<TProperty>(Expression<Func<T, TProperty>> property, Func<Faker, TProperty> setter);

      /// <summary>
      /// Creates a rule for a property.
      /// </summary>
      TFaker RuleFor<TProperty>(Expression<Func<T, TProperty>> property, Func<TProperty> valueFunction);

      /// <summary>
      /// Ignore a property or field when using StrictMode.
      /// </summary>
      TFaker Ignore<TPropertyOrField>(Expression<Func<T, TPropertyOrField>> propertyOrField);

      /// <summary>
      /// Ensures all properties of T have rules.
      /// </summary>
      /// <param name="ensureRulesForAllProperties">Overrides any global setting in Faker.DefaultStrictMode</param>
      TFaker StrictMode(bool ensureRulesForAllProperties);

      /// <summary>
      /// Creates a rule for a property.
      /// </summary>
      TFaker RuleFor<TProperty>(Expression<Func<T, TProperty>> property, TProperty value);

      /// <summary>
      /// Gives you a way to specify multiple rules inside an action
      /// without having to call RuleFor multiple times. Note: StrictMode
      /// must be false since property rules cannot be individually checked.
      /// </summary>
      TFaker Rules(Action<Faker, T> setActions);
   }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bogus
{
   /// <inheritdoc />
   public class FakerAsync<T> : FakerBase<T, FakerAsync<T>> where T : class
   {
      protected internal Dictionary<string, Func<Faker, Task<T>>> CreateActions = new(StringComparer.OrdinalIgnoreCase);
      protected internal readonly Dictionary<string, Rule<Func<Faker, T, Task>>> FinalizeActions = new(StringComparer.OrdinalIgnoreCase);

      /// <inheritdoc />
      public FakerAsync() : this("en", null)
      {
      }

      /// <inheritdoc />
      public FakerAsync(string locale) : this(locale, null)
      {
      }

      /// <inheritdoc />
      public FakerAsync(string locale = "en", IBinder binder = null) : base(locale, binder)
      {
         CreateActions[Default] = _ => Task.FromResult(Activator.CreateInstance<T>());
      }

      /// <inheritdoc />
      public override FakerAsync<T> Clone()
      {
         var clone = new FakerAsync<T>(Locale, binder);
         CloneInternal(clone);
         foreach (var root in this.CreateActions)
         {
            clone.CreateActions[root.Key] = root.Value;
         }
         foreach (var root in this.FinalizeActions)
         {
            clone.FinalizeActions.Add(root.Key, root.Value);
         }
         return clone;
      }

      /// <summary>
      /// Instructs <seealso cref="Faker{T}"/> to use the factory method as a source
      /// for new instances of <typeparamref name="T"/>.
      /// </summary>
      public virtual FakerAsync<T> CustomInstantiator(Func<Faker, Task<T>> factoryMethod)
      {
         CreateActions[currentRuleSet] = factoryMethod;
         return this;
      }

      /// <summary>
      /// Populates an instance of <typeparamref name="T"/> according to the rules
      /// defined in this <seealso cref="Faker{T}"/>.
      /// </summary>
      /// <param name="instance">The instance of <typeparamref name="T"/> to populate.</param>
      /// <param name="ruleSets">A comma separated list of rule sets to execute.
      ///    Note: The name `default` is the name of all rules defined without an explicit rule set.
      ///    When a custom rule set name is provided in <paramref name="ruleSets"/> as parameter,
      ///    the `default` rules will not run. If you want rules without an explicit rule set to run
      ///    you'll need to include the `default` rule set name in the comma separated
      ///    list of rules to run. (ex: "ruleSetA, ruleSetB, default")
      /// </param>
      public virtual Task Populate(T instance, string ruleSets = null)
      {
         var cleanRules = ParseDirtyRulesSets(ruleSets);
         return PopulateInternal(instance, cleanRules);
      }

      private Task PopulateInternal(T instance, string[] ruleSets)
      {
         base.PopulateInternal(instance, ruleSets);

         var tasks = new List<Task>(ruleSets.Length);
         foreach (var ruleSet in ruleSets)
         {
            if (FinalizeActions.TryGetValue(ruleSet, out var finalizer))
            {
               tasks.Add(finalizer.Action(FakerHub, instance));
            }
         }

         return Task.WhenAll(tasks.ToArray());
      }

      /// <summary>
      /// A finalizing action rule applied to <typeparamref name="T"/> after all the rules
      /// are executed.
      /// </summary>
      public virtual FakerAsync<T> FinishWith(Func<Faker, T, Task> action)
      {
         FinalizeActions[currentRuleSet] = new Rule<Func<Faker, T, Task>>
         {
            Action = action,
            RuleSet = currentRuleSet
         };
         return this;
      }

      /// <summary>
      /// Generates a fake object of <typeparamref name="T"/> using the specified rules in this
      /// <seealso cref="Faker{T}"/>.
      /// </summary>
      /// <param name="ruleSets">A comma separated list of rule sets to execute.
      ///    Note: The name `default` is the name of all rules defined without an explicit rule set.
      ///    When a custom rule set name is provided in <paramref name="ruleSets"/> as parameter,
      ///    the `default` rules will not run. If you want rules without an explicit rule set to run
      ///    you'll need to include the `default` rule set name in the comma separated
      ///    list of rules to run. (ex: "ruleSetA, ruleSetB, default")
      /// </param>
      public async Task<T> Generate(string ruleSets = null)
      {
         var cleanRules = ParseDirtyRulesSets(ruleSets);

         Func<Faker, Task<T>> createRule;
         if (string.IsNullOrWhiteSpace(ruleSets))
         {
            createRule = CreateActions[Default];
         }
         else
         {
            var firstRule = cleanRules[0];
            createRule = CreateActions.TryGetValue(firstRule, out createRule) ? createRule : CreateActions[Default];
         }

         //Issue 143 - We need a new FakerHub context before calling the
         //            constructor. Associated Issue 57: Again, before any
         //            rules execute, we need a context to capture IndexGlobal
         //            and IndexFaker variables.
         FakerHub.NewContext();
         var instance = await createRule(FakerHub).ConfigureAwait(false);

         await PopulateInternal(instance, cleanRules).ConfigureAwait(false);

         return instance;
      }

      /// <summary>
      /// Generates a <seealso cref="List{T}"/> fake objects of type <typeparamref name="T"/> using the specified rules in
      /// this <seealso cref="Faker{T}"/>.
      /// </summary>
      /// <param name="count">The number of items to create in the <seealso cref="List{T}"/>.</param>
      /// <param name="ruleSets">A comma separated list of rule sets to execute.
      ///    Note: The name `default` is the name of all rules defined without an explicit rule set.
      ///    When a custom rule set name is provided in <paramref name="ruleSets"/> as parameter,
      ///    the `default` rules will not run. If you want rules without an explicit rule set to run
      ///    you'll need to include the `default` rule set name in the comma separated
      ///    list of rules to run. (ex: "ruleSetA, ruleSetB, default")
      /// </param>
      public virtual async Task<T[]> Generate(int count, string ruleSets = null)
      {
         var items = new T[count];
         for (var i = 0; i < count; i++)
         {
            items[i] = await Generate(ruleSets).ConfigureAwait(false);
         }
         return items;
      }

      /// <summary>
      /// Returns an <see cref="IEnumerable{T}"/> that can be used as an unlimited source
      /// of <typeparamref name="T"/> when iterated over. Useful for generating unlimited
      /// amounts of data in a memory efficient way. Generated values *should* be repeatable
      /// for a given seed when starting with the first item in the sequence.
      /// </summary>
      /// <param name="ruleSets">A comma separated list of rule sets to execute.
      ///    Note: The name `default` is the name of all rules defined without an explicit rule set.
      ///    When a custom rule set name is provided in <paramref name="ruleSets"/> as parameter,
      ///    the `default` rules will not run. If you want rules without an explicit rule set to run
      ///    you'll need to include the `default` rule set name in the comma separated
      ///    list of rules to run. (ex: "ruleSetA, ruleSetB, default")
      /// </param>
      public virtual IEnumerable<Task<T>> GenerateForever(string ruleSets = null)
      {
         while (true)
         {
            yield return Generate(ruleSets);
         }
      }

      /// <summary>
      /// Returns an <seealso cref="IEnumerable{T}"/> with LINQ deferred execution. Generated values
      /// are not guaranteed to be repeatable until <seealso cref="Enumerable.ToList{T}"/> is called.
      /// </summary>
      /// <param name="count">The number of items to create in the <seealso cref="IEnumerable{T}"/>.</param>
      /// <param name="ruleSets">A comma separated list of rule sets to execute.
      ///    Note: The name `default` is the name of all rules defined without an explicit rule set.
      ///    When a custom rule set name is provided in <paramref name="ruleSets"/> as parameter,
      ///    the `default` rules will not run. If you want rules without an explicit rule set to run
      ///    you'll need to include the `default` rule set name in the comma separated
      ///    list of rules to run. (ex: "ruleSetA, ruleSetB, default")
      /// </param>
      public virtual IEnumerable<Task<T>> GenerateLazy(int count, string ruleSets = null)
      {
         return Enumerable.Range(1, count)
            .Select(i => Generate(ruleSets));
      }
   }
}

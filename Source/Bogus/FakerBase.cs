using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Bogus.Extensions;

namespace Bogus
{
   /// <summary>
   /// Generates fake objects of <typeparamref name="T"/>.
   /// </summary>
   /// <typeparam name="T">The object to fake.</typeparam>
   public abstract class FakerBase<T, TFaker> : IFakerTInternal, ILocaleAware, IRuleSet<T, TFaker> 
      where T : class
      where TFaker : FakerBase<T, TFaker>
   {
#pragma warning disable 1591
      protected const string Default = "default";
      private static readonly string[] DefaultRuleSet = {Default};
      protected internal Faker FakerHub;
      protected internal IBinder binder;

      protected internal readonly MultiDictionary<string, string, PopulateAction<T>> Actions =
         new MultiDictionary<string, string, PopulateAction<T>>(StringComparer.OrdinalIgnoreCase);

      protected internal readonly Dictionary<string, MemberInfo> TypeProperties;
      protected internal readonly Dictionary<string, Action<T, object>> SetterCache = new Dictionary<string, Action<T, object>>(StringComparer.OrdinalIgnoreCase);
      
      protected internal Dictionary<string, bool> StrictModes = new Dictionary<string, bool>();
      protected internal bool? IsValid;
      protected internal string currentRuleSet = Default;
      protected internal int? localSeed; // if null, the global Randomizer.Seed is used.
#pragma warning restore 1591

      Faker IFakerTInternal.FakerHub => this.FakerHub;

      IBinder IFakerTInternal.Binder => this.binder;

      int? IFakerTInternal.LocalSeed => this.localSeed;

      Type IFakerTInternal.TypeOfT => typeof(T);

      /// <summary>
      /// Clones the internal state of a <seealso cref="Faker{T}"/> into a new <seealso cref="Faker{T}"/> so that
      /// both are isolated from each other. The clone will have internal state
      /// reset as if <seealso cref="Generate(string)"/> was never called.
      /// </summary>
      public abstract TFaker Clone();


      public TFaker CloneInternal(TFaker clone)
      {
         //copy internal state.
         //strict modes.
         foreach( var root in this.StrictModes )
         {
            clone.StrictModes.Add(root.Key, root.Value);
         }

         //actions
         foreach( var root in this.Actions )
         {
            foreach( var kv in root.Value )
            {
               clone.Actions.Add(root.Key, kv.Key, kv.Value);
            }
         }

         if( localSeed.HasValue )
         {
            clone.UseSeed(localSeed.Value);
         }

         return clone;
      }

      /// <summary>
      /// The current locale.
      /// </summary>
      public string Locale { get; set; }

      /// <summary>
      /// Creates a Faker with default 'en' locale.
      /// </summary>
      protected FakerBase() : this("en", null)
      {
      }

      /// <summary>
      /// Creates a Faker with a locale
      /// </summary>
      protected FakerBase(string locale) : this(locale, null)
      {
      }

      /// <summary>
      /// Creates a Faker with a locale.
      /// </summary>
      /// <param name="locale">language</param>
      /// <param name="binder">A binder that discovers properties or fields on T that are candidates for faking. Null uses the default Binder.</param>
      protected FakerBase(string locale = "en", IBinder binder = null)
      {
         this.binder = binder ?? new Binder();
         this.Locale = locale;
         FakerHub = new Faker(locale);
         TypeProperties = this.binder.GetMembers(typeof(T));
      }

      /// <summary>
      /// Creates a seed locally scoped within this <seealso cref="Faker{T}"/> ignoring the globally scoped <seealso cref="Randomizer.Seed"/>.
      /// If this method is never called the global <seealso cref="Randomizer.Seed"/> is used.
      /// </summary>
      /// <param name="seed">The seed value to use within this <seealso cref="Faker{T}"/> instance.</param>
      public virtual TFaker UseSeed(int seed)
      {
         this.localSeed = seed;
         this.FakerHub.Random = new Randomizer(seed);
         return (TFaker) this;
      }

      /// <summary>
      /// Creates a rule for a compound property and providing access to the instance being generated.
      /// </summary>
      public virtual TFaker RuleFor<TProperty>(Expression<Func<T, TProperty>> property, Func<Faker, T, TProperty> setter)
      {
         var propName = PropertyName.For(property);

         return (TFaker) AddRule(propName, (f, t) => setter(f, t));
      }

      /// <summary>
      /// Creates a rule for a property.
      /// </summary>
      public virtual TFaker RuleFor<TProperty>(Expression<Func<T, TProperty>> property, TProperty value)
      {
         var propName = PropertyName.For(property);

         return (TFaker) AddRule(propName, (f, t) => value);
      }

      /// <summary>
      /// Creates a rule for a property.
      /// </summary>
      public virtual TFaker RuleFor<TProperty>(Expression<Func<T, TProperty>> property, Func<TProperty> valueFunction)
      {
         var propName = PropertyName.For(property);

         return (TFaker) AddRule(propName, (f, t) => valueFunction());
      }

      /// <summary>
      /// Creates a rule for a property.
      /// </summary>
      public virtual TFaker RuleFor<TProperty>(Expression<Func<T, TProperty>> property, Func<Faker, TProperty> setter)
      {
         var propName = PropertyName.For(property);

         return (TFaker) AddRule(propName, (f, t) => setter(f));
      }

      /// <summary>
      /// Create a rule for a hidden property or field.
      /// Used in advanced scenarios to create rules for hidden properties or fields.
      /// </summary>
      /// <param name="propertyOrFieldName">The property name or field name of the member to create a rule for.</param>
      public virtual TFaker RuleFor<TProperty>(string propertyOrFieldName, Func<Faker, TProperty> setter)
      {
         EnsureMemberExists(propertyOrFieldName,
            $"The property or field {propertyOrFieldName} was not found on {typeof(T)}. " +
            $"Can't create a rule for {typeof(T)}.{propertyOrFieldName} when {propertyOrFieldName} " +
            $"cannot be found. Try creating a custom IBinder for FakerBase<T> with the appropriate " +
            $"System.Reflection.BindingFlags that allows deeper reflection into {typeof(T)}.");

         return (TFaker) AddRule(propertyOrFieldName, (f, t) => setter(f));
      }

      /// <summary>
      /// Create a rule for a hidden property or field.
      /// Used in advanced scenarios to create rules for hidden properties or fields.
      /// </summary>
      /// <param name="propertyOrFieldName">The property name or field name of the member to create a rule for.</param>
      public virtual TFaker RuleFor<TProperty>(string propertyOrFieldName, Func<Faker, T, TProperty> setter)
      {
         EnsureMemberExists(propertyOrFieldName,
            $"The property or field {propertyOrFieldName} was not found on {typeof(T)}. " +
            $"Can't create a rule for {typeof(T)}.{propertyOrFieldName} when {propertyOrFieldName} " +
            $"cannot be found. Try creating a custom IBinder for FakerBase<T> with the appropriate " +
            $"System.Reflection.BindingFlags that allows deeper reflection into {typeof(T)}.");
         
         return (TFaker) AddRule(propertyOrFieldName, (f, t) => setter(f, t));
      }

      protected virtual TFaker AddRule(string propertyOrField, Func<Faker, T, object> invoker)
      {
         var rule = new PopulateAction<T>
            {
               Action = invoker,
               RuleSet = currentRuleSet,
               PropertyName = propertyOrField,
            };

         this.Actions.Add(currentRuleSet, propertyOrField, rule);

         return (TFaker) this;
      }

      /// <summary>
      /// Specify multiple rules inside an action without having to call
      /// RuleFor multiple times. Note: <seealso cref="StrictMode"/> must be false
      /// since rules for properties and fields cannot be individually checked when
      /// using this method.
      /// </summary>
      public virtual TFaker Rules(Action<Faker, T> setActions)
      {
         Func<Faker, T, object> invoker = (f, t) =>
            {
               setActions(f, t);
               return null;
            };
         var guid = Guid.NewGuid().ToString();
         var rule = new PopulateAction<T>
            {
               Action = invoker,
               RuleSet = currentRuleSet,
               PropertyName = guid,
               ProhibitInStrictMode = true
            };
         this.Actions.Add(currentRuleSet, guid, rule);
         return (TFaker) this;
      }

      /// <summary>
      /// Creates one rule for all types of <typeparamref name="TType"/> on type <typeparamref name="T"/>.
      /// In other words, if you have <typeparamref name="T"/> with many fields or properties of
      /// type <seealso cref="Int32"/> this method allows you to specify a rule for all fields or
      /// properties of type <seealso cref="Int32"/>.
      /// </summary>
      public virtual TFaker RuleForType<TType>(Type type, Func<Faker, TType> setterForType)
      {
         if( typeof(TType) != type )
         {
            throw new ArgumentException($"{nameof(TType)} must be the same type as parameter named '{nameof(type)}'");
         }

         foreach( var kvp in this.TypeProperties )
         {
            var propOrFieldType = GetFieldOrPropertyType(kvp.Value);
            var propOrFieldName = kvp.Key;

            if( propOrFieldType == type )
            {
               RuleFor(propOrFieldName, setterForType);
            }
         }

         return (TFaker) this;
      }

      /// <summary>
      /// Utility method to get the Type of a Property or Field
      /// </summary>
      protected virtual Type GetFieldOrPropertyType(MemberInfo mi)
      {
         if( mi is PropertyInfo pi )
         {
            return pi.PropertyType;
         }
         if( mi is FieldInfo fi )
         {
            return fi.FieldType;
         }
         return null;
      }

      /// <summary>
      /// Defines a set of rules under a specific name. Useful for defining
      /// rules for special cases. Note: The name `default` is the name of all rules that are
      /// defined without an explicit rule set.
      /// </summary>
      /// <param name="ruleSetName">The rule set name.</param>
      /// <param name="action">The set of rules to apply when this rules set is specified.</param>
      public virtual TFaker RuleSet(string ruleSetName, Action<IRuleSet<T, TFaker>> action)
      {
         if( currentRuleSet != Default ) throw new ArgumentException("Cannot create a rule set within a rule set.");
         currentRuleSet = ruleSetName;
         action(this);
         currentRuleSet = Default;
         return (TFaker) this;
      }

      /// <summary>
      /// Ensures a member exists provided by the IBinder.
      /// </summary>
      protected virtual void EnsureMemberExists(string propNameOrField, string exceptionMessage)
      {
         if (!this.TypeProperties.TryGetValue(propNameOrField, out MemberInfo mi))
         {
            throw new ArgumentException(exceptionMessage);
         }
      }

      /// <summary>
      /// Ignores a property or field when <seealso cref="StrictMode"/> is enabled.
      /// Used in advanced scenarios to ignore hidden properties or fields.
      /// </summary>
      /// <param name="propertyOrFieldName">The property name or field name of the member to create a rule for.</param>
      public virtual TFaker Ignore(string propertyOrFieldName)
      {
         EnsureMemberExists(propertyOrFieldName,
            $"The property or field {propertyOrFieldName} was not found on {typeof(T)}. " +
            $"Can't ignore member {typeof(T)}.{propertyOrFieldName} when {propertyOrFieldName} " +
            $"cannot be found. Try creating a custom IBinder for FakerBase<T> with the appropriate " +
            $"System.Reflection.BindingFlags that allows deeper reflection into {typeof(T)}.");

         var rule = new PopulateAction<T>
            {
               Action = null,
               RuleSet = currentRuleSet,
               PropertyName = propertyOrFieldName
            };

         this.Actions.Add(currentRuleSet, propertyOrFieldName, rule);

         return (TFaker) this;
      }

      /// <summary>
      /// Ignores a property or field when <seealso cref="StrictMode"/> is enabled.
      /// </summary>
      public virtual TFaker Ignore<TPropertyOrField>(Expression<Func<T, TPropertyOrField>> propertyOrField)
      {
         var propNameOrField = PropertyName.For(propertyOrField);

         return Ignore(propNameOrField);
      }

      /// <summary>
      /// When set to true, ensures all properties and public fields of <typeparamref name="T"/> have rules
      /// before an object of <typeparamref name="T"/> is populated or generated. Manual assertion
      /// can be invoked using <seealso cref="Validate"/> and <seealso cref="AssertConfigurationIsValid"/>.
      /// </summary>
      /// <param name="ensureRulesForAllProperties">Overrides any global setting in <seealso cref="Faker.DefaultStrictMode"/>.</param>
      public virtual TFaker StrictMode(bool ensureRulesForAllProperties)
      {
         this.StrictModes[currentRuleSet] = ensureRulesForAllProperties;
         return (TFaker) this;
      }

      /// <summary>
      /// Utility method to parse out rule sets form user input.
      /// </summary>
      protected virtual string[] ParseDirtyRulesSets(string dirtyRules)
      {
         dirtyRules = dirtyRules?.Trim(',').Trim();
         if( string.IsNullOrWhiteSpace(dirtyRules) ) return DefaultRuleSet;
         return dirtyRules.Split(',')
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim()).ToArray();
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
      protected virtual void PopulateInternal(T instance, string[] ruleSets)
      {
         ValidationResult vr = null;
         if( !IsValid.HasValue )
         {
            //run validation
            vr = ValidateInternal(ruleSets);
            this.IsValid = vr.IsValid;
         }
         if( !IsValid.GetValueOrDefault() )
         {
            throw MakeValidationException(vr ?? ValidateInternal(ruleSets));
         }

         lock( Randomizer.Locker.Value )
         {
            //Issue 57 - Make sure you generate a new context
            //           before executing any rules.
            //Issue 143 - If the FakerHub doesn't have any context
            //            (eg NewContext() has never been called), then call it
            //            so we can increment IndexGlobal and IndexFaker.
            if( !this.FakerHub.HasContext ) FakerHub.NewContext();

            foreach( var ruleSet in ruleSets )
            {
               if( this.Actions.TryGetValue(ruleSet, out var populateActions) )
               {
                  foreach( var action in populateActions.Values )
                  {
                     PopulateProperty(instance, action);
                  }
               }
            }
         }
      }

      private readonly object _setterCreateLock = new object();
      private void PopulateProperty(T instance, PopulateAction<T> action)
      {
         var valueFactory = action.Action;
         if (valueFactory is null) return; // An .Ignore() rule.

         var value = valueFactory(FakerHub, instance);
         
         if (SetterCache.TryGetValue(action.PropertyName, out var setter))
         {
            setter(instance, value);
            return;
         }
         
         if (!TypeProperties.TryGetValue(action.PropertyName, out var member)) return;
         if (member == null) return; // Member would be null if this was a .Rules()
                                     // The valueFactory is already invoked
                                     // which does not select a property or field.

         lock (_setterCreateLock)
         {
            if (SetterCache.TryGetValue(action.PropertyName, out setter))
            {
               setter(instance, value);
               return;
            }

            if (member is PropertyInfo prop)
               setter = prop.CreateSetter<T>();
            // TODO FieldInfo will need to rely on ILEmit to create a delegate 
            else if (member is FieldInfo field)
               setter = (i, v) => field?.SetValue(i, v);
            if (setter == null) return;
               
            SetterCache.Add(action.PropertyName, setter);
            setter(instance, value);
         }
      }
      /// <summary>
      /// When <seealso cref="StrictMode"/> is enabled, checks if all properties or fields of <typeparamref name="T"/> have
      /// rules defined. Returns true if all rules are defined, false otherwise.
      /// The difference between <seealso cref="Validate"/> and <seealso cref="AssertConfigurationIsValid"/>
      /// is that <seealso cref="Validate"/> will *not* throw <seealso cref="ValidationException"/>
      /// if some rules are missing when <seealso cref="StrictMode"/> is enabled.
      /// </summary>
      /// <returns>True if validation passes, false otherwise.</returns>
      public virtual bool Validate(string ruleSets = null)
      {
         var rules = ruleSets == null
            ? this.Actions.Keys.ToArray()
            : ParseDirtyRulesSets(ruleSets);
         var result = ValidateInternal(rules);
         return result.IsValid;
      }

      /// <summary>
      /// Asserts that all properties have rules. When <seealso cref="StrictMode"/> is enabled, an exception will be raised
      /// with complete list of missing rules. Useful in unit tests to catch missing rules at development
      /// time. The difference between <seealso cref="Validate"/> and <seealso cref="AssertConfigurationIsValid"/>
      /// is that <seealso cref="AssertConfigurationIsValid"/> will throw <seealso cref="ValidationException"/>
      /// if some rules are missing when <seealso cref="StrictMode"/> is enabled. <seealso cref="Validate"/>
      /// will not throw an exception and will return <seealso cref="bool"/> true or false accordingly if
      /// rules are missing when <seealso cref="StrictMode"/> is enabled.
      /// </summary>
      /// <exception cref="ValidationException"/>
      public virtual void AssertConfigurationIsValid(string ruleSets = null)
      {
         string[] rules;
         if( ruleSets is null )
         {
            rules = this.Actions.Keys.ToArray();
         }
         else
         {
            rules = ParseDirtyRulesSets(ruleSets);
         }

         var result = ValidateInternal(rules);
         if( !result.IsValid )
         {
            throw MakeValidationException(result);
         }
      }

      /// <summary>
      /// Composes a <see cref="ValidationException"/> based on the failed validation
      /// results that can be readily used to raise the exception.
      /// </summary>
      protected virtual ValidationException MakeValidationException(ValidationResult result)
      {
         var builder = new StringBuilder();

         result.ExtraMessages.ForEach(m =>
            {
               builder.AppendLine(m);
               builder.AppendLine();
            });

         builder.AppendLine("Validation was called to ensure all properties / fields have rules.")
            .AppendLine($"There are missing rules for FakerBase<T> '{typeof(T).Name}'.")
            .AppendLine("=========== Missing Rules ===========");

         foreach( var fieldOrProp in result.MissingRules )
         {
            builder.AppendLine(fieldOrProp);
         }

         return new ValidationException(builder.ToString().Trim());
      }

      private ValidationResult ValidateInternal(string[] ruleSets)
      {
         var result = new ValidationResult {IsValid = true};

         var binderPropsOrFieldsOfT = this.TypeProperties.Keys;
         foreach( var rule in ruleSets )
         {
            if( this.StrictModes.TryGetValue(rule, out var strictMode) )
            {
            }
            else
            {
               strictMode = Faker.DefaultStrictMode;
            }

            //If strictMode is not enabled, skip and move on to the next ruleSet.
            if( !strictMode ) continue;

            this.Actions.TryGetValue(rule, out var populateActions);

            var userSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if( populateActions != null )
            {
               userSet.UnionWith(populateActions.Keys);
            }

            //Get the set properties or fields that are only
            //known to the binder, while removing
            //items in userSet that are known to both the user and binder.

            userSet.SymmetricExceptWith(binderPropsOrFieldsOfT);

            //What's left in userSet is the set of properties or fields
            //that the user does not know about + .Rule() methods.

            if( userSet.Count > 0 )
            {
               foreach( var propOrFieldOfT in userSet )
               {
                  if( populateActions is not null && populateActions.TryGetValue(propOrFieldOfT, out var populateAction) )
                  {
                     // Very much a .Rules() action
                     if( populateAction.ProhibitInStrictMode )
                     {
                        result.ExtraMessages.Add(
                           $"When StrictMode is set to True the Faker<{typeof(T).Name}>.Rules(...) method cannot verify that all properties have rules. You need to use Faker<{typeof(T).Name}>.RuleFor( x => x.Prop, ...) for each property to ensure each property has an associated rule when StrictMode is true; otherwise, set StrictMode to False in order to use Faker<{typeof(T).Name}>.Rules() method.");
                        result.IsValid = false;
                     }
                  }
                  else //The user doesn't know about this property or field. Log it as a validation error.
                  {
                     result.MissingRules.Add(propOrFieldOfT);
                     result.IsValid = false;
                  }
               }
            }
         }
         return result;
      }


      /// <summary>
      /// Not Implemented: This method only exists as a work around for Visual Studio IntelliSense. See: https://github.com/bchavez/Bogus/issues/54
      /// </summary>
      [Obsolete("This exists here only as a Visual Studio IntelliSense work around. See: https://github.com/bchavez/Bogus/issues/54", true)]
      public void RuleFor<TProperty>(Expression<Func<T, TProperty>> property)
      {
         throw new NotImplementedException();
      }
   }
}
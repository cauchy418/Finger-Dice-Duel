using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// In-memory trial memento. Clones the pure model graph, including pending turn
    /// continuations and seat references; never copies Unity objects or RNG sequences.
    /// No disk serialization, scene reload or replay of tutorial actions is involved.
    /// </summary>
    internal sealed class CampaignCheckpoint
    {
        private readonly Dictionary<object,object> _copies=new Dictionary<object,object>(new ReferenceComparer());
        public static CampaignLesson Copy(CampaignLesson source)=>(CampaignLesson)new CampaignCheckpoint().Clone(source);
        private object Clone(object source)
        {
            if(source==null)return null;
            Type type=source.GetType();
            if(type.IsPrimitive||type.IsEnum||source is string||source is decimal||source is Type||source is MemberInfo||source is CharacterDefinition||source is CharacterIdentity||source is CharacterProfession)return source;
            if(_copies.TryGetValue(source,out var existing))return existing;
            if(source is Random){var random=new Random();_copies[source]=random;return random;}
            if(source is Delegate call)
            {
                Delegate combined=null;
                foreach(var item in call.GetInvocationList())
                {
                    if(item.Target!=null&&External(item.Target.GetType()))continue;
                    var target=Clone(item.Target);
                    var copy=item.Target==null?item:Delegate.CreateDelegate(item.GetType(),target,item.Method);
                    combined=Delegate.Combine(combined,copy);
                }
                _copies[source]=combined;return combined;
            }
            if(source is Array array)
            {
                var copy=(Array)array.Clone();_copies[source]=copy;
                if(array.Rank!=1)throw new InvalidOperationException("Unsupported campaign checkpoint array rank");
                for(int i=0;i<array.Length;i++)copy.SetValue(Clone(array.GetValue(i)),i);
                return copy;
            }
            if(source is IDictionary dictionary)
            {
                var copy=(IDictionary)Activator.CreateInstance(type);_copies[source]=copy;
                foreach(DictionaryEntry entry in dictionary)copy.Add(Clone(entry.Key),Clone(entry.Value));
                return copy;
            }
            if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(HashSet<>))
            {
                object copy=Activator.CreateInstance(type);_copies[source]=copy;
                var add=type.GetMethod("Add");foreach(var entry in (IEnumerable)source)add.Invoke(copy,new[]{Clone(entry)});
                return copy;
            }
            // Other system services are immutable policy infrastructure; model lists,
            // queues, value tuples and compiler-generated closures are copied below.
            if(External(type))return source;
            var result=FormatterServices.GetUninitializedObject(type);_copies[source]=result;
            for(Type t=type;t!=null;t=t.BaseType)
                foreach(var field in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    if(field.Name=="_trialBaseline")continue;
                    field.SetValue(result,Clone(field.GetValue(source)));
                }
            return result;
        }
        private static bool External(Type type)
        {
            string ns=type.Namespace??"";
            if(ns.StartsWith("Unity")||ns.StartsWith("FingerDiceDuel.UI"))return true;
            if(type.Assembly==typeof(CampaignLesson).Assembly)return false;
            if(type.IsGenericType)
            {var generic=type.GetGenericTypeDefinition();if(generic==typeof(List<>)||generic==typeof(Queue<>)||generic==typeof(Stack<>)||ns.StartsWith("System.ValueTuple"))return false;}
            return true;
        }
        private sealed class ReferenceComparer:IEqualityComparer<object>
        {public new bool Equals(object a,object b)=>ReferenceEquals(a,b);public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);}
    }
}

using UnityEngine;

using System;
using Newtonsoft.Json;
using Unity.Mathematics;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using System.Collections.Generic;
public static class StaticMethods
{
    public static int GetStableHashCode(this string str)
    {
        unchecked
        {
            int hash1 = 5381;
            int hash2 = hash1;

            for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }
    }

    public const float Epsilon = 0.00001f;

    public static bool AreCollinear(Vector2 v1, Vector2 v2)
    {
        float crossProduct = v1.x * v2.y - v1.y * v2.x;

        return Mathf.Abs(crossProduct) < 0.0001f;
    }
    
}
public class UnityMathematicsConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        string name = objectType.Name;
        return objectType == typeof(int3) || 
               objectType == typeof(int2) || 
               objectType == typeof(float3) || 
               objectType == typeof(quaternion) ||
               name.Contains("FixedList");
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is int2 i2)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(i2.x);
            writer.WritePropertyName("y"); writer.WriteValue(i2.y);
            writer.WriteEndObject();
        }
        else if (value is int3 i3)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(i3.x);
            writer.WritePropertyName("y"); writer.WriteValue(i3.y);
            writer.WritePropertyName("z"); writer.WriteValue(i3.z);
            writer.WriteEndObject();
        }
        else if (value is float3 f3)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(f3.x);
            writer.WritePropertyName("y"); writer.WriteValue(f3.y);
            writer.WritePropertyName("z"); writer.WriteValue(f3.z);
            writer.WriteEndObject();
        }
        else if (value is quaternion q)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(q.value.x);
            writer.WritePropertyName("y"); writer.WriteValue(q.value.y);
            writer.WritePropertyName("z"); writer.WriteValue(q.value.z);
            writer.WritePropertyName("w"); writer.WriteValue(q.value.w);
            writer.WriteEndObject();
        }
        else if (value.GetType().Name.Contains("FixedList"))
        {
            writer.WriteStartArray();
            var type = value.GetType();
            // Получаем свойство Length и метод GetItem через рефлексию
            var lengthProp = type.GetProperty("Length");
            int length = (int)lengthProp.GetValue(value);
            var getItemMethod = type.GetMethod("get_Item");

            for (int i = 0; i < length; i++)
            {
                var item = getItemMethod.Invoke(value, new object[] { i });
                serializer.Serialize(writer, item);
            }
            writer.WriteEndArray();
        }
    }

   public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;

        // Обработка FixedList
        if (objectType.Name.Contains("FixedList"))
        {
            var array = JArray.Load(reader);
            // Добавляем [0], так как GetGenericArguments возвращает массив
            Type elementType = objectType.GetGenericArguments()[0];
            
            // Создаем экземпляр (boxing произойдет здесь)
            object fixedList = Activator.CreateInstance(objectType);
            var addMethod = objectType.GetMethod("Add");

            foreach (var item in array)
            {
                var convertedItem = item.ToObject(elementType, serializer);
                // Для структур Invoke работает корректно, если объект в переменной типа object
                addMethod.Invoke(fixedList, new object[] { convertedItem });
            }
            return fixedList; // Возвращаем измененный упакованный объект
        }

        // Обработка Mathematics типов
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.Object)
        {
            JObject jo = (JObject)token;
            if (objectType == typeof(int2))
                return new int2((int)(jo["x"] ?? 0), (int)(jo["y"] ?? 0));
            if (objectType == typeof(int3))
                return new int3((int)(jo["x"] ?? 0), (int)(jo["y"] ?? 0),(int)(jo["z"] ?? 0));
            if (objectType == typeof(float3))
                return new float3((float)(jo["x"] ?? 0), (float)(jo["y"] ?? 0), (float)(jo["z"] ?? 0));
            if (objectType == typeof(quaternion))
                return new quaternion((float)(jo["x"] ?? 0), (float)(jo["y"] ?? 0), (float)(jo["z"] ?? 0), (float)(jo["w"] ?? 0));
        }

        return null;
    }
}
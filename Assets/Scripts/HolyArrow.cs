using UnityEngine;

public class HolyArrow : ArrowDef
{
    public int healingAmount = 2; // Количество здоровья, которое восстанавливается

    protected override void OnCollisionEnter2D(Collision2D coll)
    {
        if (coll.gameObject.TryGetComponent<Enemy>(out var enemy))
        {
            // Передаем true для святой стрелы
            enemy.TakeDamage(damage, true);
            
            //Debug.Log($"Святая стрела попала в {coll.gameObject.name}.");
        }
        else
        {
           // Debug.Log($"Святая стрела попала в {coll.gameObject.name}, но это не враг.");
        }
        if (coll.gameObject.TryGetComponent<SceletonDef>(out var SceletonDef))
        {
            // Передаем true для святой стрелы
            SceletonDef.TakeDamage(damage, true);

            //Debug.Log($"Святая стрела попала в {coll.gameObject.name}.");
        }
        else
        {
            //Debug.Log($"Святая стрела попала в {coll.gameObject.name}, но это не враг.");
        }
        if (coll.gameObject.TryGetComponent<SceletMag>(out var SceletMag))
        {
            // Передаем true для святой стрелы
            SceletMag.TakeDamage(damage, true);

            //Debug.Log($"Святая стрела попала в {coll.gameObject.name}.");
        }
        else
        {
            //Debug.Log($"Святая стрела попала в {coll.gameObject.name}, но это не враг.");
        }

        if (coll.gameObject.TryGetComponent<Slime>(out var slime))
        {
            // Передаем true для святой стрелы
            slime.TakeDamage(damage, true);

            //Debug.Log($"Святая стрела попала в {coll.gameObject.name}.");
        }
        else
        {
            //Debug.Log($"Святая стрела попала в {coll.gameObject.name}, но это не враг.");
        }
        Destroy(gameObject); // Уничтожаем стрелу после попадания
    }
}

using System;

namespace Tests {
    public class Program
    {
        public static void Main(string[] args)
        {
            var consumer = new Consumer();
            Console.WriteLine("Consumer name: " + consumer.GetName());
            Console.WriteLine("Consumer age: " + consumer.GetAge());
            Console.WriteLine("Consumer is old? " + consumer.isOld);

            var child = new ConsumerChild();
            Console.WriteLine("Child name: " + child.GetName());
            Console.WriteLine("Child age: " + child.GetAge());
            Console.WriteLine("Child is old? " + child.isOld);
        }
    }

    public class PersonMixin {

        public bool isOld {
            get {
                return GetAge() > 5;
            }
        }

        public string GetName() {
            return "Mixin";
        }

        public int GetAge() {
            return 32;
        }
    }

    [Mixin.Mixin(typeof(PersonMixin))]
    public partial class Consumer 
    {
        public Consumer() {}

        public bool isOld {
            get {
                return GetAge() > 40;
            }
        }

        public string GetName() {
            return "Consumer";
        }
    }

    [Mixin.Mixin(typeof(PersonMixin))]
    public partial class ConsumerChild : Consumer
    {
        public string GetName() {
            return "Child";
        }
        public int GetAge() {
            return 10;
        }
    }
}
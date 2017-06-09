package com.bson.random

import org.scalatest.FunSuite

/**
	* Created by bjornandersson on 2017-04-13.
	*/
class RandomTest extends FunSuite {
	test("can mixin sql dialects") {

		//
		// Implementation
		class IUseSQLAccess(sqlAccess: SQLAccess) {
			def listThoseThings = sqlAccess.listSomething.map("I use " + _)
		}

		class SQLAccess extends SQLBehaviour {
			def listSomething: Seq[String] = {
				listQuery(Seq("A", "B"))
			}
		}

		trait MSSQLBehaviour extends SQLBehaviour {
			override def listQuery(result: Seq[String]) = result.map(verb => s"MSSQL: $verb")
		}

		trait MySQLBehaviour extends SQLBehaviour {
			override def listQuery(result: Seq[String]) = result.map(verb => s"MySQL: $verb")
		}

		trait SQLBehaviour {
			def listQuery(result: Seq[String]): Seq[String] = result
		}

		//
		// Act
		val mysql = new SQLAccess with MySQLBehaviour
		mysql.listSomething.foreach(println)

		val mssql = new SQLAccess with MSSQLBehaviour
		mssql.listSomething.foreach(println)

		val thing = new IUseSQLAccess(new SQLAccess with MySQLBehaviour)
		thing.listThoseThings.foreach(println)
	}

	test("can sql dialects by provider") {

		//
		// Implementation
		class IUseSQLAccess(sqlAccess: SQLAccess) {
			def listThoseThings: Seq[String] = sqlAccess.listSomething.map("I use " + _)
		}

		class SQLAccess(dialect: SQLDialect) {
			def listSomething: Seq[String] = dialect.listQuery(Seq("A", "B"))
		}

		object MySQLDialect extends SQLDialect {
			override def listQuery(result: Seq[String]): Seq[String] = result.map("MySQL " + _)
		}

		object MSSQLDialect extends SQLDialect {
			override def listQuery(result: Seq[String]): Seq[String] = result.map("MSSQL " + _)
		}

		trait SQLDialect {
			def listQuery(result: Seq[String]): Seq[String] = result
		}


		def sqlBehaviourFactory(dialect: String): SQLDialect = {
			dialect match {
				case "MySQL" => MySQLDialect
				case "MSSQL" => MSSQLDialect
				case _ => MySQLDialect
			}
		}

		//
		// Act
		val mysql = new SQLAccess(sqlBehaviourFactory("MySQL"))
		mysql.listSomething.foreach(println)

		val mssql = new SQLAccess(sqlBehaviourFactory("MSSQL"))
		mssql.listSomething.foreach(println)

		val mssqlTypo = new SQLAccess(sqlBehaviourFactory("MSSQLSucks"))
		mssqlTypo.listSomething.foreach(println)

		val thing = new IUseSQLAccess(new SQLAccess(MySQLDialect))
		thing.listThoseThings.foreach(println)

	}

	test("what are those case classes") {

		val p1 = Person("Björn", "Andersson")
		println(p1)
		val p2 = Person("Björn")
		println(p2)
		println(Person)

		val dansbandsBjorn = p2.copy(lastName = "Anderzons")
		println(dansbandsBjorn)

		println(dansbandsBjorn == dansbandsBjorn.copy())
	}

	test("Unapply pattern matching") {

		object Anagram{
			// try make sense to this
			def apply(x: String): String = x
			def unapply(z: String): Option[String] = if (z.compareToIgnoreCase(z.reverse) == 0) Some(z) else None
		}

		def testAnagram(a:String) = a match {
			case Anagram(_) => println(s"'$a' is an anagram!")
			case _ => println(s"'$a' is not an anagram")
		}

		testAnagram("börje")
		testAnagram("Anna")


		def testAnagramB(a:String) = a match {
			case Anagram(_) => true
			case _ => false
		}

		val testStrings = Seq("anna", "Anna", "bjarne", "Kuk", "kUk")

		testStrings.filter(testAnagramB).foreach(println)

		testStrings.map(s => Anagram(s)).foreach(println)
		testStrings.map(s => Anagram.unapply(s)).foreach(println)
		testStrings.map(Anagram.unapply).foreach(println)
		testStrings.flatten(Anagram.unapply).foreach(println)

	}



	test("fold that thing"){

    val things: Seq[Int] = Seq(1,2,3,4,5)

    // First we reduce
    val sumOfThings
      = things.reduce((acc, cur) => {
        acc + cur
      })

    def sum(acc: Int, cur: Int) = acc + cur

		val sumOfThingsTwo
      = things.reduce(sum)

    assert(sumOfThingsTwo == 15)

    // then we fold
    val sumOfAllThingsFolded
      = things.foldLeft(0)(sum)
	  assert(sumOfAllThingsFolded == 15)
  }

  test("fold that practical"){
    // try to replace a foreach that adds to a list
    // instead fold a collection and add to the product which in this case is a list

    val toBeFolded = Seq("A", "BB", "CCC")

    val same0 = toBeFolded.foldLeft(List[String]())((list, cur) => {
      list :+ cur
    })

    same0.foreach(println _)


    val seq = Seq[String]()
    val same1 = toBeFolded.foldLeft(seq)((list, cur) => {
      list :+ cur
    })

    same1.foreach(println _)


    val same2 = toBeFolded.foldLeft(Seq[Int]())((list, cur) => {
      list :+ cur.length()
    })

    same2.foreach(println _)

    // Stupid
    val result3 = toBeFolded.foldLeft(Seq[Int]())((list, cur) => {
      list :+ cur.length()
    }).reduce((p, v) => p + v)

    println(result3)

    // Better
    val result4 = toBeFolded.map(_.length).reduce(_ + _)
    assert(result3 == result4)

    // Better for C# noobs
    val result5 = toBeFolded.map(str => str.length).reduce((acc, cur) => acc + cur)
    assert(result3 == result4)
  }
}


case class Person(firstName:String, lastName: String)
object Person {
	def apply(firstName: String): Person = Person(firstName,  "Doe")

	//def unapply(arg: Person): Option[(String, String)] = if(arg.lastName == "Doe") None else Some(arg.firstName, arg.lastName)
}





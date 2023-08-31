package main

import "log"

type Obj struct {
	x int
	y int
}

func main() {
	obj1 := Obj{1, 2}
	obj2 := Obj{3, 4}
	x := []Obj{obj1, obj2}

	for _, element := range x {
		element.x = 5
	}

	log.Println(x)
}

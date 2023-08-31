package main

import (
	"fmt"
	"log"
	"strconv"

	"github.com/aws/aws-lambda-go/lambda"
	"github.com/aws/aws-sdk-go/aws"
	"github.com/eniac/Beldi/pkg/beldilib"
	"github.com/mitchellh/mapstructure"
)

const (
	Read   = iota
	Append = iota
)

type Operation struct {
	Optype   int
	Target   int
	Value    int
	Return   []int
	Position int
}

// Handler for single operation calls:
// func Handler(env *beldilib.Env) interface{} {
// 	var op Operation
// 	beldilib.CHECK(mapstructure.Decode(env.Input, &op))

// 	fmt.Println("Op =")
// 	fmt.Println(op)

// 	ok, item := beldilib.TPLRead(env, "data", strconv.Itoa(op.Target), []string{"V"})
// 	if !ok {
// 		panic("TPLRead failed")
// 	}

// 	var list []int
// 	beldilib.CHECK(mapstructure.Decode(item["V"], &list))

// 	// log.Println(listWrapper)

// 	// if len(item) == 0 {
// 	// 	log.Println("Length 0")
// 	// 	beldilib.Write(env, "data", strconv.Itoa(op.Target), map[expression.NameBuilder]expression.OperandBuilder{
// 	// 		expression.Name("V"): expression.Value(ListWrapper{
// 	// 			List: []int{},
// 	// 		}),
// 	// 	})
// 	// 	listWrapper = ListWrapper{
// 	// 		List: []int{},
// 	// 	}
// 	// } else {
// 	// 	log.Println("Length not 0")
// 	// }
// 	// log.Println(listWrapper.List)

// 	if op.Optype == Read {
// 		op.Return = list
// 	} else if op.Optype == Append {
// 		list = append(list, op.Value)
// 		success := beldilib.TPLWrite(env, "data", strconv.Itoa(op.Target),
// 			aws.JSONValue{"V": list})
// 		if success {
// 			log.Printf("TPLWrite successful YES")
// 		} else {
// 			log.Printf("TPLWrite successful NO")
// 		}
// 	}

// 	// res, _ := json.Marshal(op)
// 	return op
// }

// HANDLER FOR GROUPED OPS
func Handler(env *beldilib.Env) interface{} {
	log.Printf("GUID: %v", env.InstanceId)

	var ops []Operation

	log.Printf("Input: %v", env.Input)

	beldilib.CHECK(mapstructure.Decode(env.Input, &ops))

	log.Printf("Ops: %v", ops)

	// Read the list first
	ok, item := beldilib.TPLRead(env, "data", strconv.Itoa(ops[0].Target), []string{"V"})
	if !ok {
		log.Println("Couldnt read succesfully")
		return []Operation{} // Implicit error message
	}

	var list []int
	beldilib.CHECK(mapstructure.Decode(item["V"], &list))

	log.Printf("List: %v", list)

	appended := false

	for index, op := range ops {
		if op.Optype == Read {
			// GoLang works with slices instead of arrays
			// https://go.dev/blog/slices-intro
			ops[index].Return = list
		} else if op.Optype == Append {
			appended = true // We want to write the value back
			list = append(list, op.Value)
		}
	}

	appended = true // Beldi doesnt release lock if we don't write, so always rewrite the value

	if appended {
		log.Printf("Attempting to write: %v", list)
		success := beldilib.TPLWrite(env, "data", strconv.Itoa(ops[0].Target),
			aws.JSONValue{"V": list})
		if success {
			log.Printf("TPLWrite successful YES")
		} else {
			log.Println("didnt succesfully write")
			return []Operation{} // Implicit error message
		}
	}

	fmt.Printf("ops: %v", ops)

	return ops
}

func main() {
	lambda.Start(beldilib.Wrapper(Handler))
}

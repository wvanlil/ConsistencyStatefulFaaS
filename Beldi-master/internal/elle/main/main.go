package main

import (
	"fmt"
	"log"
	"regexp"
	"strconv"
	"strings"

	"github.com/aws/aws-lambda-go/events"
	"github.com/aws/aws-lambda-go/lambda"
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

const groupOps = true
const async = true

func Handler(env *beldilib.Env) interface{} {
	// handlers/hotel/hotel.go
	// var rpcInput string
	// beldilib.CHECK(mapstructure.Decode(env.Input, &rpcInput))

	log.Println("Starting v1")

	// handlers/frontend/frontend.go
	var input string
	beldilib.CHECK(mapstructure.Decode(env.Input, &input))

	// Assumption that input is of type string
	ops := ParseInput(input)

	log.Printf("Input: %v", input)
	log.Println(ops)

	beldilib.BeginTxn(env)

	if groupOps {
		// groupOps is true

		groupops := GroupOperations(ops)

		if !async {
			// async is false

			for _, groupop := range groupops {
				log.Printf("SyncInvoke %v", groupop)

				err := DataCall(env, groupop, ops, nil)
				if err {
					beldilib.AbortTxn(env)
					log.Println("Aborting")
					return events.APIGatewayProxyResponse{StatusCode: 509}
				}
			}
		} else {
			// async is true

			// var wg sync.WaitGroup
			// wg.Add(len(groupops))

			channel := make(chan bool)

			for _, groupop := range groupops {
				go DataCall(env, groupop, ops, channel)
			}

			for i := 0; i < len(groupops); i++ {
				err := <-channel
				if err {
					beldilib.AbortTxn(env)
					log.Println("Aborting async")
					return events.APIGatewayProxyResponse{StatusCode: 509}
				}
			}

			// wg.Wait()
		}

	} else {
		// groupOps is false

		for _, singleop := range ops {
			err := DataCall(env, []Operation{singleop}, ops, nil)
			if err {
				beldilib.AbortTxn(env)
				log.Println("Aborting")
				return events.APIGatewayProxyResponse{StatusCode: 509}
			}
		}

	}
	// Single element per call, problem -> read append read does not recognize append in read
	// for index, element := range ops {
	// 	log.Println(fmt.Printf("SyncInvoke %v", element))
	// 	res, _ := beldilib.SyncInvoke(env, "data", element)

	// 	var ret Operation
	// 	beldilib.CHECK(mapstructure.Decode(res, &ret))

	// 	log.Println(fmt.Printf("Result %v", ret))
	// 	ops[index] = ret
	// }

	// List of elements per call
	// var wg sync.WaitGroup
	// wg.Add(len(mapops))
	// log.Println(fmt.Printf("Wait group size %v", len(mapops)))

	// wg.Wait()
	log.Println(ops)

	beldilib.CommitTxn(env)

	retstring := FormString(ops)

	// Done when gateway wasnt working
	// return events.APIGatewayProxyResponse{Body: retstring, StatusCode: 200}

	return retstring
}

// Maybe just return some values and insert them after committing the transaction
func DataCall(env *beldilib.Env, groupop []Operation, ops []Operation, channel chan bool) bool {
	res, _ := beldilib.SyncInvoke(env, "data", groupop)
	log.Printf("Response: %v", res)
	var ret []Operation
	beldilib.CHECK(mapstructure.Decode(res, &ret))

	if len(ret) == 0 {
		if channel != nil {
			channel <- true
		}
		return true
	}

	for _, element := range ret {
		ops[element.Position] = element
	}
	if channel != nil {
		channel <- false
	}
	return false
}

func GroupOperations(ops []Operation) map[int][]Operation {
	buckets := map[int][]Operation{}

	for _, element := range ops {
		oplist, exists := buckets[element.Target]
		if !exists {
			oplist = []Operation{}
		}
		oplist = append(oplist, element)
		buckets[element.Target] = oplist
	}

	log.Println(fmt.Printf("Buckets: %v", buckets))
	return buckets
}

// Parses the jepsen elle string into a set of operations
func ParseInput(str string) []Operation {
	re := regexp.MustCompile("\\[[^\\[\\]]+?\\]")
	ops := re.FindAllString(str, -1)

	var ops_res []Operation

	for index, element := range ops {
		segments := strings.Split(element[1:len(element)-1], " ")

		op := new(Operation)

		if segments[0] == ":r" {
			op.Optype = Read
		} else if segments[0] == ":append" {
			op.Optype = Append
		}

		op.Target, _ = strconv.Atoi(segments[1])

		if op.Optype == Append {
			op.Value, _ = strconv.Atoi(segments[2])
		}

		op.Position = index

		ops_res = append(ops_res, *op)
	}

	return ops_res
}

// Generates the string ready for jepsen elle to interpret
func FormString(ops []Operation) string {
	ret := "["
	for _, element := range ops {
		ret += "["

		if element.Optype == Read {
			ret += ":r "
		} else if element.Optype == Append {
			ret += ":append "
		}

		ret += strconv.Itoa(element.Target)

		ret += " "

		if element.Optype == Read {
			ret += "["
			for _, element2 := range element.Return {
				ret += strconv.Itoa(element2)
				ret += " "
			}
			if len(element.Return) > 0 {
				ret = ret[:len(ret)-1]
			}
			ret += "]"
		} else if element.Optype == Append {
			ret += strconv.Itoa(element.Value)
		}

		ret += "] "
	}

	ret = ret[:len(ret)-1]
	ret += "]"

	return ret
}

func main() {
	lambda.Start(beldilib.Wrapper(Handler))
}

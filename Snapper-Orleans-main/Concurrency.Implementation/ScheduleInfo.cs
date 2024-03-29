﻿using Utilities;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace Concurrency.Implementation
{
    public class ScheduleInfo
    {
        public Dictionary<int, ScheduleNode> nodes;
        public Dictionary<int, NonDeterministicBatchSchedule> nonDetBatchScheduleMap;
        public Dictionary<int, int> nonDetTxnToScheduleMap;

        public ScheduleInfo()
        {
            nodes = new Dictionary<int, ScheduleNode>();
            var node = new ScheduleNode(-1, true);
            nonDetBatchScheduleMap = new Dictionary<int, NonDeterministicBatchSchedule>();
            nonDetTxnToScheduleMap = new Dictionary<int, int>();
            node.executionPromise.SetResult(true);
            nodes.Add(-1, node);
        }

        ScheduleNode findTail()
        {
            var node = nodes[-1];
            while (node.next != null) node = node.next;
            return node;
        }

        public ScheduleNode insertNonDetTransaction(int tid)
        {
            // if tid has accessed this grain before
            if (nonDetTxnToScheduleMap.ContainsKey(tid)) return nodes[nonDetTxnToScheduleMap[tid]].prev;
            var tail = findTail();
            if (tail.isDet)
            {
                var node = new ScheduleNode(tid, false);
                var schedule = new NonDeterministicBatchSchedule(tid);
                nonDetBatchScheduleMap.Add(tid, schedule);
                nodes.Add(tid, node);
                tail.next = node;
                node.prev = tail;
                tail = node;
            }
            else
            {
                //Join the non-det tail, replace the promise
                if (tail.executionPromise.Task.IsCompleted) tail.executionPromise = new TaskCompletionSource<bool>();
            }
            nonDetBatchScheduleMap[tail.id].AddTransaction(tid);
            nonDetTxnToScheduleMap.Add(tid, tail.id);
            return tail.prev;
        }

        public void insertDetBatch(DeterministicBatchSchedule schedule)
        {
            ScheduleNode node;
            if (!nodes.ContainsKey(schedule.bid))
            {
                node = new ScheduleNode(schedule.bid, true);
                nodes.Add(schedule.bid, node);
            }
            else node = nodes[schedule.bid];

            if (nodes.ContainsKey(schedule.lastBid))
            {
                var prevNode = nodes[schedule.lastBid];
                if (prevNode.next == null)
                {
                    prevNode.next = node;
                    node.prev = prevNode;
                }
                else
                {
                    Debug.Assert(prevNode.next.isDet == false && prevNode.next.next == null);
                    prevNode.next.next = node;
                    node.prev = prevNode.next;
                }
            }
            else
            {
                // last node is already deleted because it's committed
                if (schedule.highestCommittedBid >= schedule.lastBid)
                {
                    var prevNode = nodes[-1];
                    if (prevNode.next == null)
                    {
                        prevNode.next = node;
                        node.prev = prevNode;
                    }
                    else
                    {
                        Debug.Assert(prevNode.next.isDet == false && prevNode.next.next == null);
                        prevNode.next.next = node;
                        node.prev = prevNode.next;
                    }
                }
                else   // last node hasn't arrived yet
                {
                    var prevNode = new ScheduleNode(schedule.lastBid, true);
                    nodes.Add(schedule.lastBid, prevNode);
                    prevNode.next = node;
                    node.prev = prevNode;
                    Debug.Assert(prevNode.prev == null);
                }
            }
        }

        public ScheduleNode getDependingNode(int id, bool isDet)
        {
            if (isDet) return nodes[id].prev;
            else return nodes[nonDetTxnToScheduleMap[id]].prev;
        }

        public Tuple<int, int, bool> getBeforeAfter(int tid)   // <max, min, isConsecutive>
        {
            var node = nodes[nonDetTxnToScheduleMap[tid]];
            var prevNode = node.prev;
            var maxBeforeBid = prevNode.id;

            int minAfterBid;
            if (node.next != null)
            {
                Debug.Assert(node.next.isDet);
                minAfterBid = node.next.id;
                return new Tuple<int, int, bool>(maxBeforeBid, minAfterBid, true);
            }

            minAfterBid = -1;
            foreach (var key in nodes.Keys)
            {
                if (nodes[key].isDet && key > maxBeforeBid)
                {
                    if (minAfterBid == -1) minAfterBid = key;
                    else minAfterBid = Math.Min(minAfterBid, key);
                }
            } 
            return new Tuple<int, int, bool>(maxBeforeBid, minAfterBid, false);    // both may be -1
        }

        public void completeDeterministicBatch(int id)
        {
            nodes[id].executionPromise.SetResult(true);
        }

        public void completeTransaction(int tid)   // when commit/abort a non-det txn
        {
            if (!nonDetTxnToScheduleMap.ContainsKey(tid)) return;
            var scheduleId = nonDetTxnToScheduleMap[tid];
            nonDetTxnToScheduleMap.Remove(tid);
            var schedule = nonDetBatchScheduleMap[scheduleId];

            // return true if transaction list is empty
            if (schedule.RemoveTransaction(tid)) nodes[scheduleId].executionPromise.SetResult(true);
        }
    }

    public class ScheduleNode
    {
        public int id;
        public bool isDet = false;
        public TaskCompletionSource<bool> executionPromise = new TaskCompletionSource<bool>(false);
        public ScheduleNode prev;
        public ScheduleNode next;

        public ScheduleNode(int id, bool isDet)
        {
            this.id = id;
            this.isDet = isDet;
        }
    }
}